namespace TLinkWebPortal.Services.Models
{
    /// <summary>
    /// State of a single zone (belongs to session, associated with partitions).
    /// </summary>
    public class ZoneState
    {
        public byte ZoneNumber { get; set; }
        public string? ZoneName { get; set; }
        public bool IsOpen { get; set; }
        public bool IsFaulted { get; set; }
        public bool IsTampered { get; set; }
        public bool IsBypassed { get; set; }
        public List<byte> Partitions { get; set; } = new(); // Which partitions this zone is associated with
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}