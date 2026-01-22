// DSC TLink - a communications library for DSC Powerseries NEO alarm panels
// Copyright (C) 2024 Brian Humlicek
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using DSC.TLink.Extensions;
using DSC.TLink.ITv2.Encryption;
using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Messages;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Pipelines;

namespace DSC.TLink.ITv2
{
    internal partial class ITv2Session : IAsyncDisposable
    {
        private readonly ILogger _log;
        private readonly TLinkClient _tlinkClient;
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;
        private readonly List<ITransaction> _transactions = new();
        private readonly SemaphoreSlim _transactionSemaphore = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();
        
        private int _localSequence;
        private byte _remoteSequence;
        private EncryptionHandler? _encryptionHandler;
        private int _disposed;

        public ITv2Session(TLinkClient tlinkClient, IMediator mediator, IConfiguration configuration, ILogger<ITv2Session> logger)
        {
            _tlinkClient = tlinkClient ?? throw new ArgumentNullException(nameof(tlinkClient));
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _log = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Listen for incoming messages and process them through transactions.
        /// </summary>
        /// <param name="transport">The duplex pipe for communication</param>
        /// <param name="cancellationToken">External cancellation token</param>
        public async Task ListenAsync(IDuplexPipe transport, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            _tlinkClient.InitializeTransport(transport);
            
            // Combine external token with internal shutdown token
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            var linkedToken = linkedCts.Token;

            _log.LogInformation("ITv2 session started, listening for messages");

            try
            {
                while (!linkedToken.IsCancellationRequested)
                {
                    ITv2Message message;
                    
                    try
                    {
                        message = await WaitForMessageAsync(linkedToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                    {
                        _log.LogInformation("Listen operation cancelled");
                        break;
                    }

                    // Acquire lock with timeout to prevent deadlock
                    if (!await _transactionSemaphore.WaitAsync(TimeSpan.FromSeconds(30), linkedToken).ConfigureAwait(false))
                    {
                        _log.LogError("Transaction semaphore timeout - possible deadlock");
                        throw new TimeoutException("Failed to acquire transaction lock within 30 seconds");
                    }

                    _remoteSequence = message.senderSequence;
                    
                    try
                    {
                        if (TryGetCorrelatedTransaction(message, out var pendingTransaction))
                        {
                            await pendingTransaction!.ContinueAsync(message, linkedToken).ConfigureAwait(false);
                            
                            if (pendingTransaction.Completed)
                            {
                                _transactions.Remove(pendingTransaction);
                                _log.LogDebug("Transaction completed and removed: {MessageType}", message.messageData.GetType().Name);
                            }
                        }
                        else
                        {
                            ITransaction newTransaction = TransactionFactory.CreateTransaction(message.messageData, this);
                            await newTransaction.BeginInboundAsync(message, linkedToken).ConfigureAwait(false);
                            
                            if (!newTransaction.Completed)
                            {
                                _transactions.Add(newTransaction);
                                _log.LogDebug("New transaction started: {MessageType}", message.messageData.GetType().Name);
                            }
                        }
                    }
                    finally
                    {
                        _transactionSemaphore.Release();
                    }

                    // Periodic cleanup of stale transactions
                    await CleanupStaleTransactionsAsync(linkedToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
                _log.LogInformation("ITv2 session listen loop cancelled");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Fatal error in ITv2 session listen loop");
                throw;
            }
            finally
            {
                _log.LogInformation("ITv2 session listen loop exited");
            }
        }

        /// <summary>
        /// Send a message and manage its transaction lifecycle.
        /// </summary>
        public async Task SendMessageAsync(IMessageData messageData, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            var linkedToken = linkedCts.Token;

            if (!await _transactionSemaphore.WaitAsync(TimeSpan.FromSeconds(30), linkedToken).ConfigureAwait(false))
            {
                throw new TimeoutException("Failed to acquire transaction lock for send within 30 seconds");
            }

            try
            {
                var message = new ITv2Message(
                    senderSequence: AllocateNextLocalSequence(),
                    receiverSequence: _remoteSequence,
                    messageData: messageData);

                ITransaction newTransaction = TransactionFactory.CreateTransaction(messageData, this);
                await newTransaction.BeginOutboundAsync(message, linkedToken).ConfigureAwait(false);
                
                if (!newTransaction.Completed)
                {
                    _transactions.Add(newTransaction);
                    _log.LogDebug("Outbound transaction started: {MessageType}", messageData.GetType().Name);
                }
            }
            finally
            {
                _transactionSemaphore.Release();
            }
        }

        /// <summary>
        /// Immediately shutdown the session, cancelling all pending operations.
        /// </summary>
        public async Task ShutdownAsync()
        {
            if (_shutdownCts.IsCancellationRequested)
            {
                _log.LogWarning("Shutdown already initiated");
                return;
            }

            _log.LogInformation("Initiating ITv2 session shutdown");

            // Cancel all operations
            _shutdownCts.Cancel();

            // Wait for transaction lock and abort all transactions
            if (await _transactionSemaphore.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false))
            {
                try
                {
                    var transactionCount = _transactions.Count;
                    foreach (var transaction in _transactions.ToArray())
                    {
                        try
                        {
                            transaction.Abort();
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning(ex, "Error aborting transaction during shutdown");
                        }
                    }
                    _transactions.Clear();
                    _log.LogInformation("Aborted {Count} pending transactions", transactionCount);
                }
                finally
                {
                    _transactionSemaphore.Release();
                }
            }
            else
            {
                _log.LogWarning("Could not acquire transaction lock during shutdown");
            }

            _log.LogInformation("ITv2 session shutdown complete");
        }

        async Task<ITv2Message> WaitForMessageAsync(CancellationToken cancellationToken)
        {
            var payload = await WaitForClientPayloadAsync(cancellationToken).ConfigureAwait(false);
            return ParseITv2Message(payload);
        }

        async Task<byte[]> WaitForClientPayloadAsync(CancellationToken cancellationToken)
        {
            var clientResult = await _tlinkClient.ReadMessageAsync(cancellationToken).ConfigureAwait(false);

            if (clientResult.IsComplete)
            {
                _log.LogWarning("Client connection completed/closed");
                throw new TLinkPacketException(TLinkPacketException.Code.Disconnected);
            }

            return clientResult.Payload;
        }

        ITv2Message ParseITv2Message(byte[] payload)
        {
            // ITv2 Frame Structure:
            // [Length:1-2][Sender:1][Receiver:1][Command?:2][Payload:0-N][CRC:2]
            var decryptedPayload = _encryptionHandler?.HandleInboundData(payload) ?? payload;

            _log.LogTrace("Received ITv2 frame: Length={Length}", decryptedPayload.Length);

            var messageBytes = new ReadOnlySpan<byte>(decryptedPayload);
            ITv2Framing.RemoveFraming(ref messageBytes); // Removes length prefix and validates CRC

            // Sequence bytes track message ordering (wrap at 255)
            byte senderSeq = messageBytes.PopByte();      // Remote's incrementing counter
            byte receiverSeq = messageBytes.PopByte();    // Expected local sequence

            // Remaining bytes = command (ushort) + payload
            // If no command, this is a SimpleAck (protocol acknowledgment)

            return new ITv2Message(
                senderSequence: senderSeq,
                receiverSequence: receiverSeq,
                messageData: MessageFactory.DeserializeMessage(messageBytes));
        }

        bool TryGetCorrelatedTransaction(ITv2Message message, out ITransaction? transaction)
        {
            transaction = _transactions.FirstOrDefault(tx => tx.IsCorrelated(message));
            return transaction != null;
        }

        async Task SendMessageAsync(ITv2Message message, CancellationToken cancellationToken)
        {
            var messageBytes = new List<byte>(
                [message.senderSequence,
                 message.receiverSequence,
                 ..message.messageData.Serialize()
                ]);

            ITv2Framing.AddFraming(messageBytes);
            await SendMessageBytesAsync(messageBytes.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        async Task SendMessageBytesAsync(byte[] messageBytes, CancellationToken cancellationToken)
        {
            _log.LogTrace("Sending ITv2 frame: Length={Length}", messageBytes.Length);

            var encryptedBytes = _encryptionHandler?.HandleOutboundData(messageBytes) ?? messageBytes;
            await _tlinkClient.SendMessageAsync(encryptedBytes, cancellationToken).ConfigureAwait(false);
        }

        byte AllocateNextLocalSequence()
        {
            // Thread-safe sequence allocation with automatic byte wrap
            return (byte)Interlocked.Increment(ref _localSequence);
        }

        void SetEncryptionHandler(EncryptionType encryptionType)
        {
            _encryptionHandler?.Dispose();
            
            _encryptionHandler = encryptionType switch
            {
                EncryptionType.Type1 => new Type1EncryptionHandler(_configuration),
                EncryptionType.Type2 => new Type2EncryptionHandler(_configuration),
                _ => throw new NotSupportedException($"Unsupported encryption type: {encryptionType}")
            };

            _log.LogInformation("Encryption handler set to {Type}", encryptionType);
        }

        private async Task CleanupStaleTransactionsAsync(CancellationToken cancellationToken)
        {
            // Only cleanup periodically (every 100 messages processed)
            if (_localSequence % 100 != 0) return;

            if (!await _transactionSemaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false))
                return; // Skip if lock not immediately available

            try
            {
                var now = DateTime.UtcNow;
                var stale = _transactions
                    .Where(tx => tx.IsTimedOut(now))
                    .ToArray();

                foreach (var tx in stale)
                {
                    _log.LogWarning("Removing stale transaction: {Type}", tx.GetType().Name);
                    tx.Abort();
                    _transactions.Remove(tx);
                }

                if (stale.Length > 0)
                {
                    _log.LogInformation("Cleaned up {Count} stale transactions", stale.Length);
                }
            }
            finally
            {
                _transactionSemaphore.Release();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed != 0)
                throw new ObjectDisposedException(nameof(ITv2Session));
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            _log.LogInformation("Disposing ITv2 session");

            await ShutdownAsync().ConfigureAwait(false);

            _transactionSemaphore?.Dispose();
            _shutdownCts?.Dispose();
            _encryptionHandler?.Dispose();

            _log.LogInformation("ITv2 session disposed");
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        internal record ITv2Message(byte senderSequence, byte receiverSequence, IMessageData messageData);
    }
}
