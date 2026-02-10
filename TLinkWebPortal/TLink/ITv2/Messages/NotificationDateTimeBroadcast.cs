using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Transactions;
using DSC.TLink.Serialization;


namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(ITv2Command.Notification_Time_Date_Broadcast)]
    [SimpleAckTransaction]
    public record NotificationDateTimeBroadcast: IMessageData
    {
        public DateTime DateTime { get; init; }
    }
}
