using DSC.TLink.Serialization;
using static DSC.TLink.ITv2.ITv2Session;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.Notification_Life_Style_Zone_Status)]
    [SimpleAckTransaction]

    internal record NotificationLifestyleZoneStatus : IMessageData
    {
        [CompactInteger]
        public byte ZoneNumber { get; init; }
        public LifeStyleZoneStatusCode Status { get; init; }
        public enum LifeStyleZoneStatusCode : byte
        {
            Unknown = 0xFF,
            Restored = 0,
            Open = 1
        }
    }
}
