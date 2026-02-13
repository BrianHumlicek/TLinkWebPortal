namespace TLinkWebPortal.Services.Models
{
    /// <summary>
    /// State of a single partition (no longer contains zones).
    /// </summary>
    public class PartitionState
    {
        public byte PartitionNumber { get; set; }
        public bool IsReady { get; set; }
        public bool IsArmed { get; set; }
        public string ArmMode { get; set; } = "Unknown";
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}