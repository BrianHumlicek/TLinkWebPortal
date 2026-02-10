using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Transactions;
using DSC.TLink.Serialization;
using MediatR;


namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.Notification_Partition_Ready_Status)]
    [SimpleAckTransaction]
    public record NotificationPartitionReadyStatus : IMessageData, INotification
    {
        [CompactInteger]
        public byte PartitionNumber { get; init; }
        public PartitionReadyStatusEnum Status { get; init; }
        public enum PartitionReadyStatusEnum : byte
        {
            Reserved = 0,
            ReadyToArm = 1,
            ReadyToForceArm = 2,
            NotReadyToArm = 3,
        }
    }
}
