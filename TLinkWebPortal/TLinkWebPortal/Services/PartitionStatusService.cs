using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.MediatR;
using DSC.TLink.ITv2.Messages;
using MediatR;
using System.Collections.Concurrent;
using TLinkWebPortal.Services.Models;
using static DSC.TLink.ITv2.Messages.ModuleZoneStatus;

namespace TLinkWebPortal.Services
{
    /// <summary>
    /// Maintains partition and zone status state.
    /// State is updated by dedicated notification handlers.
    /// </summary>
    public class PartitionStatusService : IPartitionStatusService
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PartitionStatusService> _logger;
        
        // Session -> Partition -> PartitionState
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<byte, PartitionState>> _state = new();

        public event EventHandler<PartitionStateChangedEventArgs>? PartitionStateChanged;
        public event EventHandler<ZoneStateChangedEventArgs>? ZoneStateChanged;

        public PartitionStatusService(
            IMediator mediator,
            ILogger<PartitionStatusService> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        #region Read

        public PartitionState? GetPartition(string sessionId, byte partitionNumber)
        {
            return _state.TryGetValue(sessionId, out var partitions) &&
                   partitions.TryGetValue(partitionNumber, out var partition)
                ? partition
                : null;
        }

        public IReadOnlyDictionary<byte, PartitionState> GetPartitions(string sessionId)
        {
            return _state.TryGetValue(sessionId, out var partitions)
                ? partitions
                : new Dictionary<byte, PartitionState>();
        }

        public ZoneState? GetZone(string sessionId, byte partitionNumber, byte zoneNumber)
        {
            var partition = GetPartition(sessionId, partitionNumber);
            return partition?.Zones.TryGetValue(zoneNumber, out var zone) == true
                ? zone
                : null;
        }

        public IReadOnlyDictionary<byte, ZoneState> GetZones(string sessionId, byte partitionNumber)
        {
            var partition = GetPartition(sessionId, partitionNumber);
            return partition?.Zones ?? new Dictionary<byte, ZoneState>();
        }

        #endregion

        #region Write

        public void UpdatePartition(string sessionId, PartitionState partition)
        {
            var sessionPartitions = _state.GetOrAdd(sessionId, _ => new ConcurrentDictionary<byte, PartitionState>());
            sessionPartitions[partition.PartitionNumber] = partition;

            PartitionStateChanged?.Invoke(this, new PartitionStateChangedEventArgs
            {
                SessionId = sessionId,
                Partition = partition
            });
        }

        public void UpdateZone(string sessionId, byte partitionNumber, ZoneState zone)
        {
            var sessionPartitions = _state.GetOrAdd(sessionId, _ => new ConcurrentDictionary<byte, PartitionState>());
            var partition = sessionPartitions.GetOrAdd(partitionNumber, _ => new PartitionState { PartitionNumber = partitionNumber });
            partition.Zones[zone.ZoneNumber] = zone;

            ZoneStateChanged?.Invoke(this, new ZoneStateChangedEventArgs
            {
                SessionId = sessionId,
                PartitionNumber = partitionNumber,
                Zone = zone
            });
        }

        #endregion

        #region Commands

        /// <summary>
        /// Manually request a zone status update from the panel.
        /// Normally not needed as zone updates come automatically via NotificationLifestyleZoneStatus.
        /// </summary>
        public async Task<bool> RequestZoneStatusUpdateAsync(
            string sessionId, 
            byte partitionNumber, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Manually requesting zone status for partition {Partition} (Session: {SessionId})",
                    partitionNumber, sessionId);

                var command = new SessionCommand
                {
                    SessionID = sessionId,
                    MessageData = new CommandRequestMessage
                    {
                        CommandRequest = ITv2Command.ModuleStatus_Zone_Status,
                        Data = [0x01, 0x01, 0x07]
                    }
                };

                var response = await _mediator.Send(command, cancellationToken);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Failed to request zone status for partition {Partition}: {Error}",
                        partitionNumber, response.ErrorMessage);
                    return false;
                }

                if (response.MessageData is ModuleZoneStatus zoneStatus)
                {
                    _logger.LogDebug(
                        "Received zone status response: Start={Start}, Count={Count} (Session: {SessionId})",
                        zoneStatus.ZoneStart, zoneStatus.ZoneCount, sessionId);

                    ProcessModuleZoneStatus(sessionId, zoneStatus);
                    return true;
                }

                _logger.LogWarning(
                    "Unexpected response type for zone status request: {Type}",
                    response.MessageData?.GetType().Name ?? "null");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error requesting zone status for partition {Partition}",
                    partitionNumber);
                return false;
            }
        }

        #endregion

        #region Private Helpers

        private void ProcessModuleZoneStatus(string sessionId, ModuleZoneStatus zoneStatus)
        {
            for (int i = 0; i < zoneStatus.ZoneCount && i < zoneStatus.ZoneStatusBytes.Length; i++)
            {
                int zoneNumber = zoneStatus.ZoneStart + i;
                if (zoneNumber > 255) break;

                var statusByte = (ZoneStatusEnum)zoneStatus.ZoneStatusBytes[i];
                byte partitionNumber = DeterminePartitionForZone((byte)zoneNumber);

                var zone = GetZone(sessionId, partitionNumber, (byte)zoneNumber) 
                    ?? new ZoneState { ZoneNumber = (byte)zoneNumber };

                zone.IsOpen = statusByte.HasFlag(ZoneStatusEnum.Open);
                zone.IsFaulted = statusByte.HasFlag(ZoneStatusEnum.Fault);
                zone.IsTampered = statusByte.HasFlag(ZoneStatusEnum.Tamper);
                zone.IsBypassed = statusByte.HasFlag(ZoneStatusEnum.Bypass);
                zone.LastUpdated = DateTime.UtcNow;

                _logger.LogTrace(
                    "Zone {Zone}: Open={Open}, Fault={Fault}, Tamper={Tamper}, Bypass={Bypass}",
                    zoneNumber, zone.IsOpen, zone.IsFaulted, zone.IsTampered, zone.IsBypassed);

                UpdateZone(sessionId, partitionNumber, zone);
            }
        }

        private static byte DeterminePartitionForZone(byte zoneNumber)
        {
            // Zones 1-64 = partition 1, 65-128 = partition 2, etc.
            return (byte)Math.Max(1, (zoneNumber - 1) / 64 + 1);
        }

        #endregion
    }
}