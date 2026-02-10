namespace TLinkWebPortal.Services.Models
{
    public class PartitionState
    {
        public byte PartitionNumber { get; init; }
        public bool IsReady { get; set; }
        public bool IsArmed { get; set; }
        public string ArmMode { get; set; } = "Disarmed";
        public DateTime LastUpdated { get; set; }
        public Dictionary<byte, ZoneState> Zones { get; init; } = new();
    }

    public class ZoneState
    {
        public byte ZoneNumber { get; init; }
        public bool IsOpen { get; set; }
        public bool IsFaulted { get; set; }
        public bool IsTampered { get; set; }
        public bool IsBypassed { get; set; }
        public string ZoneName { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }
}