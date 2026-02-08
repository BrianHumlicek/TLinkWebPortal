using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Transactions;
using DSC.TLink.Serialization;

namespace DSC.TLink.ITv2.Messages
{
    [SimpleAckTransaction]
    internal record DefaultMessage : IMessageData
    {
        [IgnoreProperty]
        public ITv2Command Command { get; set; } = ITv2Command.Unknown;
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }
}
