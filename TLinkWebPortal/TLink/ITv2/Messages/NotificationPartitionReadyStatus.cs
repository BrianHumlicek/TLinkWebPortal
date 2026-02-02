using DSC.TLink.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DSC.TLink.ITv2.ITv2Session;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.Notification_Partition_Ready_Status)]
    [SimpleAckTransaction]
    internal record NotificationPartitionReadyStatus : IMessageData
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
