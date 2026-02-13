namespace TLinkWebPortal.Services.Models
{
    /// <summary>
    /// Represents a complete session (panel connection) with its partitions and zones.
    /// </summary>
    public class SessionState
    {
        public required string SessionId { get; init; }
        public string Name => SessionId; // TODO: Allow friendly names
        public Dictionary<byte, PartitionState> Partitions { get; } = new();
        public Dictionary<byte, ZoneState> Zones { get; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}