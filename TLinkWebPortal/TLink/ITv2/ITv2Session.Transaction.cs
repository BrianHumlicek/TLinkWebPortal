using DSC.TLink.ITv2.Messages;
using Microsoft.Extensions.Logging;

namespace DSC.TLink.ITv2
{
	internal partial class ITv2Session
	{
        /// <summary>
        /// Base class for all (except handshake) ITv2 protocol transactions.
        /// 
        /// Transaction Lifecycle:
        /// 1. **Creation**: TransactionFactory.CreateTransaction() based on message type
        /// 2. **Begin**: BeginInboundAsync() or BeginOutboundAsync() starts the transaction
        /// 3. **Continue**: ContinueAsync() handles subsequent correlated messages
        /// 4. **Completion**: Complete() or Abort() ends the transaction
        /// 
        /// Thread Safety:
        /// - All transactions run under ITv2Session._transactionSemaphore
        /// - Only one transaction processes at a time
        /// - Transactions can access session's private state directly
        /// 
        /// Timeout:
        /// - Each transaction has a configurable timeout (default 30s)
        /// - OnTimeout() called if transaction doesn't complete in time
        /// - Session periodically cleans up timed-out transactions
        /// </summary>
        abstract class Transaction : ITransaction
		{
			protected readonly ITv2Session session;
			private byte localSequence, remoteSequence;
            private byte? appSequence;
            private Func<ITv2Message, bool> isCorrelated = new Func<ITv2Message, bool>(message => false);

            // Timeout infrastructure
            private readonly TimeSpan _timeout;
			private readonly CancellationTokenSource _timeoutCts = new();

			protected Transaction(ITv2Session session, TimeSpan? timeout = null)
			{
				this.session = session;
                _timeout = timeout ?? Timeout.InfiniteTimeSpan;
            }

			protected abstract Task ContinueAsync(ITv2Message message, CancellationToken cancellationToken);
			protected abstract bool CanContinue { get; }
			
			protected Task SendMessageAsync(IMessageData messageData, CancellationToken cancellationToken)
			{
                // Link with timeout cancellation
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);
                var message = new ITv2Message(localSequence, remoteSequence, appSequence, messageData);
                return session.SendMessageAsync(message, linkedCts.Token);
			}
			private async Task beginInboundAsync(ITv2Message message, CancellationToken cancellationToken)
			{
                _timeoutCts.CancelAfter(_timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);
				
				remoteSequence = message.senderSequence;
                localSequence = (byte)session._localSequence; //message.receiverSequence;// session.AllocateNextLocalSequence();
                appSequence = message.appSequence;
                if (appSequence.HasValue)
                {
                    session.UpdateAppSequence(appSequence.Value);
                }
                isCorrelated = new Func<ITv2Message, bool>(msg => msg.senderSequence == remoteSequence && isCorrelatedMessage(msg));
				await InitializeInboundAsync(linkedCts.Token);
			}

			private async Task beginOutboundAsync(ITv2Message message, CancellationToken cancellationToken)
			{
                _timeoutCts.CancelAfter(_timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);
				
				localSequence = message.senderSequence;
				remoteSequence = message.receiverSequence;
                appSequence = message.appSequence;
                isCorrelated = new Func<ITv2Message, bool>(msg => msg.receiverSequence == localSequence && isCorrelatedMessage(msg));
				await session.SendMessageAsync(message, linkedCts.Token);
				await InitializeOutboundAsync(linkedCts.Token);
			}

            protected virtual bool isCorrelatedMessage(ITv2Message message) => true;
            protected abstract Task InitializeInboundAsync(CancellationToken cancellationToken);
			protected abstract Task InitializeOutboundAsync(CancellationToken cancellationToken);

			/// <summary>
			/// Abort the transaction
			/// </summary>
			protected void Abort()
			{
				session._log.LogWarning("{TransactionType} aborted", GetType().Name);
				_timeoutCts?.Cancel();
				_timeoutCts?.Dispose();
			}

            // Explicit ITransaction interface implementations
            bool ITransaction.CanContinue => CanContinue && !_timeoutCts.IsCancellationRequested;			
			Task ITransaction.BeginInboundAsync(ITv2Message message, CancellationToken cancellationToken) 
				=> beginInboundAsync(message, cancellationToken);			
			Task ITransaction.BeginOutboundAsync(ITv2Message message, CancellationToken cancellationToken) 
				=> beginOutboundAsync(message, cancellationToken);			
			async Task<bool> ITransaction.TryContinueAsync(ITv2Message message, CancellationToken cancellationToken)
			{
				if (isCorrelated.Invoke(message) && ((ITransaction)this).CanContinue)
				{
					remoteSequence = message.senderSequence;
					await ContinueAsync(message, cancellationToken);
					return true;
				}
				return false;
			}
			void ITransaction.Abort() => Abort();
		}
	}
}
