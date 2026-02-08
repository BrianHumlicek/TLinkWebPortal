using DSC.TLink.ITv2.Messages;
using Microsoft.Extensions.Logging;
using static DSC.TLink.ITv2.ITv2Session;

namespace DSC.TLink.ITv2.Transactions
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
    internal abstract class Transaction : ITransaction
	{
        private ILogger _log;
        private Func<ITv2MessagePacket, CancellationToken, Task> _sendMessageDelegate;

        private byte localSequence, remoteSequence;
        private byte? appSequence;
        private Func<ITv2MessagePacket, bool> isCorrelated = new Func<ITv2MessagePacket, bool>(message => false);

        // Timeout infrastructure
        private readonly TimeSpan _timeout;
		private readonly CancellationTokenSource _timeoutCts = new();

		protected Transaction(ILogger log, Func<ITv2MessagePacket, CancellationToken, Task> sendMessageDelegate, TimeSpan? timeout = null)
		{
            _log = log;
            _sendMessageDelegate = sendMessageDelegate;
            _timeout = timeout ?? Timeout.InfiniteTimeSpan;
        }

        protected ILogger log => _log;
        protected abstract Task ContinueAsync(ITv2MessagePacket message, CancellationToken cancellationToken);
		protected abstract bool CanContinue { get; }
			
		protected Task SendMessageAsync(IMessageData messageData, CancellationToken cancellationToken)
		{
            // Link with timeout cancellation
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);
            var message = new ITv2MessagePacket(localSequence, remoteSequence, appSequence, messageData);
            return _sendMessageDelegate(message, linkedCts.Token);
		}
		private async Task beginInboundAsync(ITv2MessagePacket message, CancellationToken cancellationToken)
		{
            _timeoutCts.CancelAfter(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);
				
			remoteSequence = message.senderSequence;
            //The following was working and im keeping this until I confirm the changes
            //localSequence = (byte)session._localSequence; //message.receiverSequence;// session.AllocateNextLocalSequence();
            localSequence = message.receiverSequence;
            appSequence = message.appSequence;
            //if (appSequence.HasValue)
            //{
            //    session.UpdateAppSequence(appSequence.Value);
            //}
            isCorrelated = inboundCorrelataion;
			await InitializeInboundAsync(linkedCts.Token);
		}

		private async Task beginOutboundAsync(ITv2MessagePacket message, CancellationToken cancellationToken)
		{
            _timeoutCts.CancelAfter(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutCts.Token);
				
			localSequence = message.senderSequence;
			remoteSequence = message.receiverSequence;
            appSequence = message.appSequence;
            isCorrelated = outboundCorrelataion;
			await _sendMessageDelegate(message, linkedCts.Token);
			await InitializeOutboundAsync(linkedCts.Token);
		}

        bool inboundCorrelataion(ITv2MessagePacket message) => message.senderSequence == remoteSequence && isCorrelatedMessage(message);
        bool outboundCorrelataion(ITv2MessagePacket message) => message.receiverSequence == localSequence && isCorrelatedMessage(message);
        protected virtual bool isCorrelatedMessage(ITv2MessagePacket message) => true;
        protected abstract Task InitializeInboundAsync(CancellationToken cancellationToken);
		protected abstract Task InitializeOutboundAsync(CancellationToken cancellationToken);

		/// <summary>
		/// Abort the transaction
		/// </summary>
		protected void Abort()
		{
			log.LogWarning("{TransactionType} aborted", GetType().Name);
			_timeoutCts?.Cancel();
			_timeoutCts?.Dispose();
		}

        // Explicit ITransaction interface implementations
        bool ITransaction.CanContinue => CanContinue && !_timeoutCts.IsCancellationRequested;			
		Task ITransaction.BeginInboundAsync(ITv2MessagePacket message, CancellationToken cancellationToken) 
			=> beginInboundAsync(message, cancellationToken);			
		Task ITransaction.BeginOutboundAsync(ITv2MessagePacket message, CancellationToken cancellationToken) 
			=> beginOutboundAsync(message, cancellationToken);			
		async Task<bool> ITransaction.TryContinueAsync(ITv2MessagePacket message, CancellationToken cancellationToken)
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