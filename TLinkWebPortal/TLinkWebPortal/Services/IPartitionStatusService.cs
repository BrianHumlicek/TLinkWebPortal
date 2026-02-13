using TLinkWebPortal.Services.Models;

namespace TLinkWebPortal.Services
{
    public interface IPartitionStatusService
    {
        // Session operations
        SessionState? GetSession(string sessionId);
        IReadOnlyDictionary<string, SessionState> GetAllSessions();

        // Partition operations (partition belongs to session)
        PartitionState? GetPartition(string sessionId, byte partitionNumber);
        IReadOnlyDictionary<byte, PartitionState> GetPartitions(string sessionId);
        void UpdatePartition(string sessionId, PartitionState partition);

        // Zone operations (zone belongs to session, not partition)
        ZoneState? GetZone(string sessionId, byte zoneNumber);
        IReadOnlyDictionary<byte, ZoneState> GetZones(string sessionId);
        void UpdateZone(string sessionId, ZoneState zone);

        // Events
        event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;
        event EventHandler<PartitionStateChangedEventArgs>? PartitionStateChanged;
        event EventHandler<ZoneStateChangedEventArgs>? ZoneStateChanged;
    }

    public class SessionStateChangedEventArgs : EventArgs
    {
        public required string SessionId { get; init; }
        public required SessionState Session { get; init; }
    }

    public class PartitionStateChangedEventArgs : EventArgs
    {
        public required string SessionId { get; init; }
        public required PartitionState Partition { get; init; }
    }

    public class ZoneStateChangedEventArgs : EventArgs
    {
        public required string SessionId { get; init; }
        public required ZoneState Zone { get; init; }
    }
}