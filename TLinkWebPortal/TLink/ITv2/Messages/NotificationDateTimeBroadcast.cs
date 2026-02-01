using static DSC.TLink.ITv2.ITv2Session;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.Notification_Time_Date_Broadcast)]
    [SimpleAckTransaction]
    internal record NotificationDateTimeBroadcast: IMessageData
    {
        public DateTime DateTime { get; init; }
    }
}
