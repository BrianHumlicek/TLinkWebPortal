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
            
            private readonly TimeSpan _timeout;
            private readonly CancellationTokenSource _timeoutCts = new();

            public HandshakeTransaction(ITv2Session session, TimeSpan? timeout = null)
            {
                this.session = session;
                state = State.ReceiveOpenSession;
                subTransaction = new CommandResponseTransaction(session);
                _timeout = timeout ?? Timeout.InfiniteTimeSpan;
            }

            private void Abort()
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

            async Task transactionStateMachine(ITv2Message message, CancellationToken cancellationToken)
            {
                // Link external cancellation with timeout cancellation
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);

                await subTransaction.TryContinueAsync(message, linkedCts.Token);

                if (!subTransaction.CanContinue)
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
                            var replymessage = this.openSessionMessage! with { AppSequence = (byte)(this.openSessionMessage!.AppSequence + 1) };
                            await subTransaction.BeginOutboundAsync(
                                new ITv2Message(
                                    session.AllocateNextLocalSequence(),
                                    session._remoteSequence,
                                    replymessage),
                                linkedCts.Token);
                            state = State.WaitForRequestAccess;
                            break;                            
                        case State.WaitForRequestAccess:
                            state = State.ReceiveRequestAccess;
                            break;
                        case State.ReceiveRequestAccess:
                            if (message.messageData is RequestAccess requestAccessMessage)
                            {
                                session._log.LogDebug("Setting outbound encryption");
                                session._encryptionHandler!.ConfigureOutboundEncryption(requestAccessMessage.Initializer);
                                await subTransaction.BeginInboundAsync(message, linkedCts.Token);
                                state = State.SendRequestAccess;
                                break;
                            }
                            session._log.LogError("Expected RequestAccess message, got {Type}", message.messageData.GetType().Name);
                            Abort();
                            throw new InvalidOperationException("Expected RequestAccess message in handshake");
                            
                        case State.SendRequestAccess:
                            session._log.LogDebug("Setting inbound encryption");
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
                            session.beginHeartBeat(cancellationToken);
                            break;
                    }
                }
            }
            
            // Explicit interface implementations delegate to private/internal members
            async Task<bool> ITransaction.TryContinueAsync(ITv2Message message, CancellationToken cancellationToken)
			{
				//The handshake is always correlated as when it is running
				//it is the only transaction that is valid to operate
				if (canContinue)
				{
					await transactionStateMachine(message, cancellationToken);
					return true;
				}
				return false;
			}
			Task ITransaction.BeginInboundAsync(ITv2Message message, CancellationToken cancellationToken)
                => transactionStateMachine(message, cancellationToken);
            Task ITransaction.BeginOutboundAsync(ITv2Message message, CancellationToken cancellationToken)
                => throw new NotImplementedException("Handshake is always initiated by remote panel");
            bool ITransaction.CanContinue => canContinue;            
            void ITransaction.Abort() => Abort();

            // Private backing implementations for interface members
            private bool canContinue => state != State.Complete && !_timeoutCts.IsCancellationRequested;
        }

        enum State
        {
            ReceiveOpenSession,
            SendOpenSession,
            WaitForRequestAccess,
            ReceiveRequestAccess,
            SendRequestAccess,
            AwaitingComplete,
            Complete
        }
    }
}
