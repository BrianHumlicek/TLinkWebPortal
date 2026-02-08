using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Transactions;
using DSC.TLink.Serialization;


namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(ITv2Command.ModuleStatus_Global_Status)]
    [SimpleAckTransaction]
    internal record GlobalStatusResponse : IMessageData
    {
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }
}
