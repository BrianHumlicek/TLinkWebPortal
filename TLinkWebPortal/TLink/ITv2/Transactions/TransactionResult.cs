using DSC.TLink.ITv2.Messages;

namespace DSC.TLink.ITv2.Transactions
{
    public record TransactionResult
    {
        public bool Success { get; init; }
        public IMessageData MessageData { get; init; }
        public string? ErrorMessage { get; init; }
        public TransactionResult() 
        { 
                Success = true;
                MessageData = null!;
                ErrorMessage = null;
        }
        public TransactionResult(IMessageData MessageData)
        {
            Success = true;
            this.MessageData = MessageData;
            ErrorMessage = null;
        }
        public TransactionResult(string ErrorMessage)
        {
            Success = false;
            MessageData = null!;
            this.ErrorMessage = ErrorMessage;
        }
    }
}
