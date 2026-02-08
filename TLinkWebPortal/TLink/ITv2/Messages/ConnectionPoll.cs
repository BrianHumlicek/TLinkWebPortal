using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Transactions;
using DSC.TLink.Serialization;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(ITv2Command.Connection_Poll)]
    [SimpleAckTransaction]
    internal record ConnectionPoll : IMessageData
    {
    }
}
