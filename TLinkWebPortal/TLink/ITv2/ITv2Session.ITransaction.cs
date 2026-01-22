namespace DSC.TLink.ITv2
{
    internal partial class ITv2Session
    {
        private interface ITransaction
        {
            /// <summary>
            /// Indicates whether the transaction has completed (successfully or not).
            /// </summary>
            bool Completed { get; }

            /// <summary>
            /// Start an inbound transaction (initiated by remote device).
            /// </summary>
            Task BeginInboundAsync(ITv2Session.ITv2Message message, CancellationToken cancellationToken);

            /// <summary>
            /// Start an outbound transaction (initiated by local server).
            /// </summary>
            Task BeginOutboundAsync(ITv2Session.ITv2Message message, CancellationToken cancellationToken);

            /// <summary>
            /// Continue an existing transaction with a new message.
            /// </summary>
            Task ContinueAsync(ITv2Session.ITv2Message message, CancellationToken cancellationToken);

            /// <summary>
            /// Check if this transaction correlates with the given message.
            /// </summary>
            bool IsCorrelated(ITv2Session.ITv2Message message);

            /// <summary>
            /// Check if this transaction has exceeded its timeout.
            /// </summary>
            bool IsTimedOut(DateTime now);

            /// <summary>
            /// Abort the transaction, cancelling any pending operations.
            /// </summary>
            void Abort();
        }
    }
}
