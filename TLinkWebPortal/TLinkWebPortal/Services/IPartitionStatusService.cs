using TLinkWebPortal.Services.Models;

namespace TLinkWebPortal.Services
{
    public interface IPartitionStatusService
    {
        /// <summary>
        /// Get the current state of a specific partition
        /// </summary>
        PartitionState? GetPartition(string sessionId, byte partitionNumber);

        /// <summary>
        /// Get all partitions for a session
        /// </summary>
        IReadOnlyDictionary<byte, PartitionState> GetPartitions(string sessionId);

        /// <summary>
        /// Get a specific zone state
        /// </summary>
        ZoneState? GetZone(string sessionId, byte partitionNumber, byte zoneNumber);

        /// <summary>
        /// Get all zones for a partition
        /// </summary>
        IReadOnlyDictionary<byte, ZoneState> GetZones(string sessionId, byte partitionNumber);

        /// <summary>
        /// Event raised when partition state changes
        /// </summary>
        event EventHandler<PartitionStateChangedEventArgs>? PartitionStateChanged;

        /// <summary>
        /// Event raised when zone state changes
        /// </summary>
        event EventHandler<ZoneStateChangedEventArgs>? ZoneStateChanged;
    }

    public class PartitionStateChangedEventArgs : EventArgs
    {
        public required string SessionId { get; init; }
        public required PartitionState Partition { get; init; }
    }

    public class ZoneStateChangedEventArgs : EventArgs
    {
        public required string SessionId { get; init; }
        public required byte PartitionNumber { get; init; }
        public required ZoneState Zone { get; init; }
    }
}