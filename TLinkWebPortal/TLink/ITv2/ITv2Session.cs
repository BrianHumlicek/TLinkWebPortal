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
using Microsoft.Extensions.Options;
using System.IO.Pipelines;
using System.Transactions;
using static DSC.TLink.ITv2.ITv2Session;

namespace DSC.TLink.ITv2
{
    internal partial class ITv2Session : IAsyncDisposable
    {
        private readonly ILogger _log;
        private readonly TLinkClient _tlinkClient;
        private readonly IMediator _mediator;
        private readonly ITv2Settings _itv2Settings;
        private readonly List<ITransaction> _waitingTransactions = new();
        private readonly SemaphoreSlim _transactionSemaphore = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();
        
        private int _localSequence = 1, _appSequence;
        private byte _remoteSequence;
        private EncryptionHandler? _encryptionHandler;
        private int _disposed;

        public ITv2Session(
            TLinkClient tlinkClient, 
            IMediator mediator, 
            IOptions<ITv2Settings> settingsOptions, 
            ILogger<ITv2Session> logger)
        {
            _tlinkClient = tlinkClient ?? throw new ArgumentNullException(nameof(tlinkClient));
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _itv2Settings = settingsOptions.Value ?? throw new ArgumentNullException(nameof(settingsOptions));
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

            _log.LogInformation("ITv2 session started, listening for messages.");

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
						bool messageHandled = false;
						foreach (var waitingTransaction in _waitingTransactions)
						{
							if (await waitingTransaction.TryContinueAsync(message, linkedToken))
							{
								messageHandled = true;
                                break;
							}
						}
                        if (!messageHandled)
                        {
                            _log.LogMessageDebug("Received", message.messageData);
                            if (message.messageData is DefaultMessage defaultMessage)
                            {
                                _log.LogWarning("Command {command}", defaultMessage.Command);
                                _log.LogWarning($"Data: {ILoggerExtensions.Enumerable2HexString(defaultMessage.Data)}");
                            }
                            ITransaction newTransaction = TransactionFactory.CreateTransaction(message.messageData, this);
							_log.LogDebug("New {TransactionType} started: {MessageType}", newTransaction.GetType().Name, message.messageData.GetType().Name);
							await newTransaction.BeginInboundAsync(message, linkedToken).ConfigureAwait(false);
                            
                            if (newTransaction.CanContinue)
                            {
                                _waitingTransactions.Add(newTransaction);
                            }
                            else
                            {
                                _log.LogDebug("Transaction completed immediately: {MessageType}", message.messageData.GetType().Name);
                            }
                        }
                        var staleTransactions = _waitingTransactions.Where(tx => !tx.CanContinue).ToList();
                        foreach (var stale in staleTransactions)
                        {
                            _log.LogDebug("Removing completed transaction: {Type}", stale.GetType().Name);
                            _waitingTransactions.Remove(stale);
                        }
                    }
                    finally
                    {
                        _transactionSemaphore.Release();
                    }

                    // Periodic cleanup of stale transactions
                    //await CleanupStaleTransactionsAsync(linkedToken).ConfigureAwait(false);
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

        void beginHeartBeat(CancellationToken cancellation)
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(10000);
                    //await SendMessageAsync(new CommandRequestMessage() { CommandRequest = ITv2Command.Connection_Software_Version });
                    await SendMessageAsync(new CommandRequestMessage() { CommandRequest = ITv2Command.ModuleStatus_Global_Status});
                    //await SendMessageAsync(new GlobalStatusResponse());
                    _log.LogDebug("Sent command request: SW Version");
                    do
                    {
                        await Task.Delay(30000, cancellation).ConfigureAwait(false);
                        await SendMessageAsync(new ConnectionPoll(), cancellation).ConfigureAwait(false);
                        _log.LogDebug("Sent Heartbeat");

                    } while (!cancellation.IsCancellationRequested);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error sending heartbeat");
                    
                }
            }, cancellation).ConfigureAwait(false);

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
                    appSequence: messageData.IsAppSequence ? AllocateNextAppSequence() : null,
                    messageData: messageData);

                ITransaction newTransaction = TransactionFactory.CreateTransaction(messageData, this);
                _log.LogMessageDebug("Sending", messageData);
                await newTransaction.BeginOutboundAsync(message, linkedToken).ConfigureAwait(false);
                
                if (newTransaction.CanContinue)
                {
                    _waitingTransactions.Add(newTransaction);
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
                    var transactionCount = _waitingTransactions.Count;
                    foreach (var transaction in _waitingTransactions.ToArray())
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
                    _waitingTransactions.Clear();
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

            var messageBytes = new ReadOnlySpan<byte>(decryptedPayload);
            ITv2Framing.RemoveFraming(ref messageBytes); // Removes length prefix and validates CRC

            if (_log.IsEnabled(LogLevel.Trace))
            {
                _log.LogTrace("Received message (post decryption) {messageBytes}", messageBytes.ToArray());
            }


            // Sequence bytes track message ordering (wrap at 255)
            byte senderSeq = messageBytes.PopByte();      // Remote's incrementing counter
            byte receiverSeq = messageBytes.PopByte();    // Expected local sequence
            (byte? appSeq, IMessageData messageData) = MessageFactory.DeserializeMessage(messageBytes);

            return new ITv2Message(
                senderSequence:   senderSeq,
                receiverSequence: receiverSeq,
                appSequence:      appSeq,
                messageData:      messageData);
        }

        async Task SendMessageAsync(ITv2Message message, CancellationToken cancellationToken)
        {
            var messageBytes = new List<byte>(
                [message.senderSequence,
                 message.receiverSequence,
                 ..message.messageData.Serialize(message.appSequence)
                ]);

            _log.LogTrace("Sending message (pre encryption) {messageBytes}", messageBytes);
            ITv2Framing.AddFraming(messageBytes);
            var encryptedBytes = _encryptionHandler?.HandleOutboundData(messageBytes.ToArray()) ?? messageBytes.ToArray();
            await _tlinkClient.SendMessageAsync(encryptedBytes, cancellationToken);
        }

        byte AllocateNextLocalSequence()
        {
            // Thread-safe sequence allocation with automatic byte wrap
            return (byte)Interlocked.Increment(ref _localSequence);
        }
        void UpdateAppSequence(byte appSequence)
        {
            _appSequence = appSequence;
        }
        byte AllocateNextAppSequence()
        {
            // Thread-safe sequence allocation with automatic byte wrap
            return (byte)Interlocked.Increment(ref _appSequence);
        }

        void SetEncryptionHandler(EncryptionType encryptionType)
        {
            _encryptionHandler?.Dispose();
            
            _encryptionHandler = encryptionType switch
            {
                EncryptionType.Type1 => new Type1EncryptionHandler(_itv2Settings),
                EncryptionType.Type2 => new Type2EncryptionHandler(_itv2Settings),
                _ => throw new NotSupportedException($"Unsupported encryption type: {encryptionType}")
            };

            _log.LogInformation("Encryption handler set to {Type}", encryptionType);
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

        internal record ITv2Message(byte senderSequence, byte receiverSequence, byte? appSequence, IMessageData messageData);
    }
}
