using DSC.TLink.ITv2.Messages;
using Microsoft.Extensions.Logging;

namespace DSC.TLink.ITv2
{
    internal partial class ITv2Session
    {
        /// <summary>
        /// ITv2 connection handshake
        /// 
        /// Protocol Flow:
        /// ┌────────┐                           ┌────────┐
        /// │ Remote │                           │ Local  │
        /// │ Panel  │                           │ Server │
        /// └───┬────┘                           └───┬────┘
        ///     │                                    │
        ///     │          1. OpenSession            │
        ///     │─────────────────────────────────>  │
        ///     │                                    │
        ///     │        2. CommandResponse (ACK)    │
        ///     │  <──────────────────────────────-──│
        ///     │                                    │
        ///     │         3. SimpleAck               │
        ///     │──────────────────────────────-──>  │
        ///     │                                    │
        ///     │         4. OpenSession             │
        ///     │  <────────────────────────────-────│
        ///     │                                    │
        ///     │        5. CommandResponse          │
        ///     │──────────────────────────────-──>  │
        ///     │                                    │
        ///     │         6. SimpleAck               │
        ///     │  <──────────────────────────────-──│
        ///     │                                    │
        ///     │        7. RequestAccess            │ remote initializer is received and outbound encryption is configured and immedietly activated
        ///     │──────────────────────────────-──>  │
        ///     │                                    │
        ///     │                                    │
        ///     │        8. CommandResponse          │
        ///     │  <═════════════════════════════════│ (encrypted)
        ///     │                                    │
        ///     │         9. SimpleAck               │
        ///     │──────────────────────────────-──>  │ (plain text)
        ///     │                                    │
        ///     │       10. RequestAccess            │ local initializer is sent and inbound encryption is configured and immedietly activated
        ///     │  <═════════════════════════════════│ (encrypted)
        ///     │                                    │
        ///     │       11. CommandResponse          │
        ///     │═════════════════════════════════>  │ (encrypted)
        ///     │                                    │
        ///     │        12. SimpleAck               │
        ///     │  <═════════════════════════════════│ (encrypted)
        ///     │                                    │
        ///     │    [Handshake Complete]            │
        ///     │    All traffic now encrypted       │
        /// 
        /// State transitions:
        /// - Step 1-3: Panel announces capabilities
        /// - Step 4-6: Server echoes session (mutual capability exchange)
        /// - Step 7-9: Panel provides encryption key for messages going to the panel
        /// - Step 10-12: Server provides encryption key for messages going to the server
        /// Each grouping of steps is handled by a CommandResponseTransaction as a sub-transaction
        /// The sequence counters increment once for each subtransaction grouping.  
        /// </summary>

        class HandshakeTransaction : ITransaction
        {
            ITv2Session session;
            State state;
            ITransaction subTransaction;
            OpenSession? openSessionMessage;
            
            // Timeout support
            private readonly DateTime _startTime;
            private readonly TimeSpan _timeout;
            private readonly CancellationTokenSource _timeoutCts;
            private int _aborted;

            // Private backing implementations for interface members
            private bool Completed => state == State.Complete || _aborted != 0;

            public HandshakeTransaction(ITv2Session session, TimeSpan? timeout = null)
            {
                this.session = session;
                state = State.ReceiveOpenSession;
                subTransaction = new CommandResponseTransaction(session);
                
                // Initialize timeout infrastructure
                _startTime = DateTime.UtcNow;
                _timeout = timeout ?? TimeSpan.FromSeconds(60); // Handshake gets 60s (longer than normal transactions)
                _timeoutCts = new CancellationTokenSource();
                
                // Schedule timeout task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(_timeout, _timeoutCts.Token);
                        if (!Completed && _aborted == 0)
                        {
                            session._log.LogWarning("Handshake transaction timed out after {Timeout}", _timeout);
                            Abort();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout cancelled - transaction completed normally
                    }
                });
            }

            private void Abort()
            {
                if (Interlocked.CompareExchange(ref _aborted, 1, 0) == 0)
                {
                    session._log.LogWarning("Handshake transaction aborted at state {State}", state);
                    _timeoutCts?.Cancel();
                    _timeoutCts?.Dispose();
                    
                    // Abort sub-transaction if active
                    ((ITransaction)subTransaction)?.Abort();
                    
                    // Clean up encryption handler if handshake failed
                    if (state != State.Complete)
                    {
                        session._encryptionHandler?.Dispose();
                        session._encryptionHandler = null;
                    }
                    
                    state = State.Complete; // Mark as complete so it gets removed
                }
            }

            async Task transactionStateMachine(ITv2Message message, CancellationToken cancellationToken)
            {
                if (_aborted != 0)
                {
                    session._log.LogWarning("Transaction aborted, ignoring message");
                    return;
                }

                // Link external cancellation with timeout cancellation
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);

                if (!subTransaction.Completed)
                {
                    await subTransaction.ContinueAsync(message, linkedCts.Token);
                }
                if (subTransaction.Completed)
                {
                    switch (state)
                    {
                        case State.ReceiveOpenSession:
                            if (message.messageData is OpenSession openSessionMessage)
                            {
                                this.openSessionMessage = openSessionMessage;
                                session.SetEncryptionHandler(openSessionMessage.EncryptionType);
                                await subTransaction.BeginInboundAsync(message, linkedCts.Token);
                                state = State.SendOpenSession;
                                break;
                            }
                            session._log.LogError("Expected OpenSession message, got {Type}", message.messageData.GetType().Name);
                            Abort();
                            throw new InvalidOperationException("Expected OpenSession message in handshake");
                            
                        case State.SendOpenSession:
                            await subTransaction.BeginOutboundAsync(
                                new ITv2Message(
                                    session.AllocateNextLocalSequence(),
                                    session._remoteSequence,
                                    this.openSessionMessage!),
                                linkedCts.Token);
                            state = State.ReceiveRequestAccess;
                            break;
                            
                        case State.ReceiveRequestAccess:
                            if (message.messageData is RequestAccess requestAccessMessage)
                            {
                                session._encryptionHandler!.ConfigureOutboundEncryption(requestAccessMessage.Initializer);
                                await subTransaction.BeginInboundAsync(message, linkedCts.Token);
                                state = State.SendRequestAccess;
                                break;
                            }
                            session._log.LogError("Expected RequestAccess message, got {Type}", message.messageData.GetType().Name);
                            Abort();
                            throw new InvalidOperationException("Expected RequestAccess message in handshake");
                            
                        case State.SendRequestAccess:
                            var requestAccess = new RequestAccess()
                            {
                                Initializer = session._encryptionHandler!.ConfigureInboundEncryption()
                            };
                            await subTransaction.BeginOutboundAsync(
                                new ITv2Message(
                                    session.AllocateNextLocalSequence(),
                                    session._remoteSequence,
                                    requestAccess),
                                linkedCts.Token);
                            state = State.AwaitingComplete;
                            break;
                            
                        case State.AwaitingComplete:
                            state = State.Complete;
                            _timeoutCts.Cancel(); // Cancel timeout on successful completion
                            session._log.LogInformation("Handshake completed successfully");
                            break;
                    }
                }
            }
            
            // Explicit interface implementations delegate to private/internal members
            async Task ITransaction.ContinueAsync(ITv2Message message, CancellationToken cancellationToken)
            {
                await transactionStateMachine(message, cancellationToken);
            }
            
            Task ITransaction.BeginInboundAsync(ITv2Message message, CancellationToken cancellationToken)
            {
                return transactionStateMachine(message, cancellationToken);
            }
            
            Task ITransaction.BeginOutboundAsync(ITv2Message message, CancellationToken cancellationToken)
            {
                throw new NotImplementedException("Handshake is always initiated by remote panel");
            }
            
            bool ITransaction.IsCorrelated(ITv2Message header)
            {
                return state != State.Complete && _aborted == 0;
            }
            
            bool ITransaction.Completed => Completed;
            
            bool ITransaction.IsTimedOut(DateTime now)
            {
                return now - _startTime > _timeout && state != State.Complete;
            }
            
            void ITransaction.Abort() => Abort();
        }

        enum State
        {
            ReceiveOpenSession,
            SendOpenSession,
            ReceiveRequestAccess,
            SendRequestAccess,
            AwaitingComplete,
            Complete
        }
    }
}
