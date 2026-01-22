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
			private Func<ITv2Message, bool> isCorrelated = new Func<ITv2Message, bool>(message => false);
			
			// Timeout infrastructure
			private readonly DateTime _startTime;
			private readonly TimeSpan _timeout;
			private readonly CancellationTokenSource _timeoutCts;
			private int _aborted;

			protected Transaction(ITv2Session session, TimeSpan? timeout = null)
			{
				this.session = session;
				
				// Initialize timeout
				_startTime = DateTime.UtcNow;
				_timeout = timeout ?? TimeSpan.FromSeconds(30); // Default 30s timeout
				_timeoutCts = new CancellationTokenSource();
				
				// Schedule timeout task
				_ = Task.Run(async () =>
				{
					try
					{
						await Task.Delay(_timeout, _timeoutCts.Token);
						if (!Completed && _aborted == 0)
						{
							session._log.LogWarning("{TransactionType} timed out after {Timeout}", GetType().Name, _timeout);
							OnTimeout();
							Abort();
						}
					}
					catch (OperationCanceledException)
					{
						// Timeout cancelled - transaction completed normally
					}
				});
			}

			protected abstract Task ContinueAsync(ITv2Message message, CancellationToken cancellationToken);
			protected abstract bool Completed { get; }
			
			protected Task SendMessageAsync(IMessageData messageData, CancellationToken cancellationToken)
			{
				// Link with timeout cancellation
				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);
                var message = new ITv2Message(localSequence, remoteSequence, messageData);
                return session.SendMessageAsync(message, linkedCts.Token);
			}

			private async Task beginInboundAsync(ITv2Message message, CancellationToken cancellationToken)
			{
				if (_aborted != 0)
				{
					session._log.LogWarning("{TransactionType} aborted, ignoring BeginInbound", GetType().Name);
					return;
				}

				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);
				
				remoteSequence = message.senderSequence;
				localSequence = session.AllocateNextLocalSequence();
				isCorrelated = new Func<ITv2Message, bool>(msg => msg.senderSequence == remoteSequence);
				await InitializeInboundAsync(linkedCts.Token);
			}

			private async Task beginOutboundAsync(ITv2Message message, CancellationToken cancellationToken)
			{
				if (_aborted != 0)
				{
					session._log.LogWarning("{TransactionType} aborted, ignoring BeginOutbound", GetType().Name);
					return;
				}

				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);
				
				localSequence = message.senderSequence;
				remoteSequence = message.receiverSequence;
				isCorrelated = new Func<ITv2Message, bool>(msg => msg.receiverSequence == localSequence);
				await session.SendMessageAsync(message, linkedCts.Token);
				await InitializeOutboundAsync(linkedCts.Token);
			}

			protected abstract Task InitializeInboundAsync(CancellationToken cancellationToken);
			protected abstract Task InitializeOutboundAsync(CancellationToken cancellationToken);

			/// <summary>
			/// Called when the transaction times out. Override to provide custom timeout handling.
			/// </summary>
			protected virtual void OnTimeout()
			{
				// Default: just log (already logged in timeout task)
			}

			/// <summary>
			/// Abort the transaction and clean up resources.
			/// </summary>
			protected void Abort()
			{
				if (Interlocked.CompareExchange(ref _aborted, 1, 0) == 0)
				{
					session._log.LogWarning("{TransactionType} aborted", GetType().Name);
					_timeoutCts?.Cancel();
					_timeoutCts?.Dispose();
					OnAbort();
				}
			}

			/// <summary>
			/// Called when the transaction is aborted. Override to clean up resources.
			/// </summary>
			protected virtual void OnAbort()
			{
				// Default: no-op
			}

			/// <summary>
			/// Mark the transaction as complete and cancel timeout.
			/// Call this from derived classes when transaction completes successfully.
			/// </summary>
			protected void Complete()
			{
				_timeoutCts?.Cancel();
			}

			// Explicit ITransaction interface implementations
			bool ITransaction.Completed => Completed || _aborted != 0;
			
			Task ITransaction.BeginInboundAsync(ITv2Message message, CancellationToken cancellationToken) 
				=> beginInboundAsync(message, cancellationToken);
			
			Task ITransaction.BeginOutboundAsync(ITv2Message message, CancellationToken cancellationToken) 
				=> beginOutboundAsync(message, cancellationToken);
			
			Task ITransaction.ContinueAsync(ITv2Message message, CancellationToken cancellationToken)
			{
				if (_aborted != 0)
				{
					session._log.LogWarning("{TransactionType} aborted, ignoring Continue", GetType().Name);
					return Task.CompletedTask;
				}
				return ContinueAsync(message, cancellationToken);
			}
			
			bool ITransaction.IsCorrelated(ITv2Message message) => isCorrelated.Invoke(message) && _aborted == 0;
			
			bool ITransaction.IsTimedOut(DateTime now) 
				=> now - _startTime > _timeout && !Completed && _aborted == 0;
			
			void ITransaction.Abort() => Abort();
		}
	}
}
