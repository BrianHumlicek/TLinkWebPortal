namespace DSC.TLink.ITv2
{
    internal partial class ITv2Session
    {
        /// <summary>
        /// Attribute to associate a message type with its transaction handler via a factory method.
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
        internal sealed class HandshakeTransactionAttribute : Attribute, ICreateTransaction
        {
            ITransaction ICreateTransaction.CreateTransaction(ITv2Session session) => new HandshakeTransaction(session);
        }
    }
}
