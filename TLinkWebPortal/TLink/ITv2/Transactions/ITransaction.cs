namespace DSC.TLink.ITv2.Transactions
{
    internal interface ITransaction
    {
        /// <summary>
        /// Indicates whether the transaction has completed (successfully or not).
        /// </summary>
        bool CanContinue { get; }

        /// <summary>
        /// Start an inbound transaction (initiated by remote device).
        /// </summary>
        Task BeginInboundAsync(ITv2MessagePacket message, CancellationToken cancellationToken);

        /// <summary>
        /// Start an outbound transaction (initiated by local server).
        /// </summary>
        Task BeginOutboundAsync(ITv2MessagePacket message, CancellationToken cancellationToken);

        /// <summary>
        /// Continue an existing transaction with a new message.
        /// </summary>
        Task<bool> TryContinueAsync(ITv2MessagePacket message, CancellationToken cancellationToken);

        /// <summary>
        /// Abort the transaction, cancelling any pending operations.
        /// </summary>
        void Abort();
    }
}