namespace DSC.TLink.ITv2
{
    internal partial class ITv2Session
    {
        private interface ICreateTransaction
        {
            ITransaction CreateTransaction(ITv2Session session);
        }
    }
}
