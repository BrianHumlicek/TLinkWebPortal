using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Transactions;
using DSC.TLink.Serialization;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(ITv2Command.Notification_Chime_Broadcast)]
    [SimpleAckTransaction]
    public record NotificationChimeBroadcast : IMessageData
    {
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }
}
