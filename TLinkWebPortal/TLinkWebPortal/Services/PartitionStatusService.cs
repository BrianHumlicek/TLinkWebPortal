using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.MediatR;
using DSC.TLink.ITv2.Messages;
using MediatR;
using System.Collections.Concurrent;
using TLinkWebPortal.Services.Models;
using static DSC.TLink.ITv2.Messages.NotificationPartitionReadyStatus;
using static DSC.TLink.ITv2.Messages.ModuleZoneStatus;

namespace TLinkWebPortal.Services
{
    /// <summary>
    /// Maintains partition and zone status state by handling notifications
    /// and requesting zone updates when partition status changes.
    /// </summary>
    public class PartitionStatusService : IPartitionStatusService,
        INotificationHandler<SessionNotification<NotificationPartitionReadyStatus>>,
        INotificationHandler<SessionNotification<NotificationLifestyleZoneStatus>>
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

        #region Public API

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

        #region Notification Handlers

        /// <summary>
        /// Handles partition status notifications and requests zone status updates
        /// </summary>
        public async Task Handle(
            SessionNotification<NotificationPartitionReadyStatus> notification,
            CancellationToken cancellationToken)
        {
            try
            {
                var msg = notification.MessageData;
                var sessionId = notification.SessionId;

                _logger.LogInformation(
                    "Partition {Partition} status: {Status} (Session: {SessionId})",
                    msg.PartitionNumber, msg.Status, sessionId);

                // Update partition state
                var partition = GetOrCreatePartition(sessionId, msg.PartitionNumber);
                
                // Interpret the ready status enum
                partition.IsReady = msg.Status == PartitionReadyStatusEnum.ReadyToArm || 
                                   msg.Status == PartitionReadyStatusEnum.ReadyToForceArm;
                
                // Store the arm mode based on status
                partition.ArmMode = msg.Status switch
                {
                    PartitionReadyStatusEnum.ReadyToArm => "Ready",
                    PartitionReadyStatusEnum.ReadyToForceArm => "Ready (Force)",
                    PartitionReadyStatusEnum.NotReadyToArm => "Not Ready",
                    _ => "Unknown"
                };
                
                partition.LastUpdated = notification.ReceivedAt;

                // Raise event for UI
                PartitionStateChanged?.Invoke(this, new PartitionStateChangedEventArgs
                {
                    SessionId = sessionId,
                    Partition = partition
                });

                // Request zone status update for this partition
                await RequestZoneStatusUpdate(sessionId, msg.PartitionNumber, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling partition status notification");
            }
        }

        /// <summary>
        /// Handles single zone lifestyle notifications (real-time zone changes)
        /// </summary>
        public async Task Handle(
            SessionNotification<NotificationLifestyleZoneStatus> notification,
            CancellationToken cancellationToken)
        {
            try
            {
                var msg = notification.MessageData;
                var sessionId = notification.SessionId;

                _logger.LogDebug(
                    "Received lifestyle zone status for zone {Zone}: {Status} (Session: {SessionId})",
                    msg.ZoneNumber, msg.Status, sessionId);

                // Determine which partition this zone belongs to
                byte partitionNumber = DeterminePartitionForZone(msg.ZoneNumber);

                var partition = GetOrCreatePartition(sessionId, partitionNumber);
                var zone = GetOrCreateZone(partition, msg.ZoneNumber);

                // Update zone state based on lifestyle status
                zone.IsOpen = msg.Status == NotificationLifestyleZoneStatus.LifeStyleZoneStatusCode.Open;
                zone.LastUpdated = notification.ReceivedAt;

                _logger.LogDebug(
                    "Zone {Zone} is now {Status} (Partition: {Partition}, Session: {SessionId})",
                    zone.ZoneNumber, zone.IsOpen ? "OPEN" : "CLOSED", partitionNumber, sessionId);

                // Raise event for UI
                ZoneStateChanged?.Invoke(this, new ZoneStateChangedEventArgs
                {
                    SessionId = sessionId,
                    PartitionNumber = partitionNumber,
                    Zone = zone
                });

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling lifestyle zone status notification");
            }
        }

        #endregion

        #region Private Helpers

        private PartitionState GetOrCreatePartition(string sessionId, byte partitionNumber)
        {
            var sessionPartitions = _state.GetOrAdd(sessionId, _ => new ConcurrentDictionary<byte, PartitionState>());
            return sessionPartitions.GetOrAdd(partitionNumber, _ => new PartitionState
            {
                PartitionNumber = partitionNumber
            });
        }

        private ZoneState GetOrCreateZone(PartitionState partition, byte zoneNumber)
        {
            if (!partition.Zones.TryGetValue(zoneNumber, out var zone))
            {
                zone = new ZoneState { ZoneNumber = zoneNumber };
                partition.Zones[zoneNumber] = zone;
            }
            return zone;
        }

        private async Task RequestZoneStatusUpdate(string sessionId, byte partitionNumber, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Requesting zone status for partition {Partition} (Session: {SessionId})",
                    partitionNumber, sessionId);

                // Send CommandRequestMessage to get zone status
                var command = new SessionCommand
                {
                    SessionID = sessionId,
                    MessageData = new CommandRequestMessage
                    {
                        CommandRequest = ITv2Command.ModuleStatus_Zone_Status,
                        Data = new byte[] 
                        { 
                            0x01, // Module/partition number
                            0x01, // Start zone
                            0x10  // Zone count (64 zones max)
                        }
                    }
                };

                // Send command and wait for response
                var response = await _mediator.Send(command, cancellationToken);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Failed to request zone status for partition {Partition}: {Error}",
                        partitionNumber, response.ErrorMessage);
                    return;
                }

                // Process the ModuleZoneStatus response
                if (response.MessageData is ModuleZoneStatus zoneStatus)
                {
                    _logger.LogDebug(
                        "Received zone status response: Start={Start}, Count={Count} (Session: {SessionId})",
                        zoneStatus.ZoneStart, zoneStatus.ZoneCount, sessionId);

                    // Process each zone's status byte
                    for (int i = 0; i < zoneStatus.ZoneCount && i < zoneStatus.ZoneStatusBytes.Length; i++)
                    {
                        int zoneNumber = zoneStatus.ZoneStart + i;
                        if (zoneNumber > 255) break; // Validate zone number

                        var statusByte = (ZoneStatusEnum)zoneStatus.ZoneStatusBytes[i];
                        
                        // Determine partition for this zone
                        byte zonePart = DeterminePartitionForZone((byte)zoneNumber);
                        
                        var partition = GetOrCreatePartition(sessionId, zonePart);
                        var zone = GetOrCreateZone(partition, (byte)zoneNumber);

                        // Update all zone properties from status flags
                        zone.IsOpen = statusByte.HasFlag(ZoneStatusEnum.Open);
                        zone.IsFaulted = statusByte.HasFlag(ZoneStatusEnum.Fault);
                        zone.IsTampered = statusByte.HasFlag(ZoneStatusEnum.Tamper);
                        zone.IsBypassed = statusByte.HasFlag(ZoneStatusEnum.Bypass);
                        zone.LastUpdated = DateTime.UtcNow;

                        _logger.LogTrace(
                            "Zone {Zone}: Open={Open}, Fault={Fault}, Tamper={Tamper}, Bypass={Bypass}",
                            zoneNumber, zone.IsOpen, zone.IsFaulted, zone.IsTampered, zone.IsBypassed);

                        // Raise event for UI
                        ZoneStateChanged?.Invoke(this, new ZoneStateChangedEventArgs
                        {
                            SessionId = sessionId,
                            PartitionNumber = zonePart,
                            Zone = zone
                        });
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Unexpected response type for zone status request: {Type}",
                        response.MessageData?.GetType().Name ?? "null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error requesting zone status for partition {Partition}",
                    partitionNumber);
            }
        }

        private byte DeterminePartitionForZone(byte zoneNumber)
        {
            // Simple mapping: zones 1-64 = partition 1, 65-128 = partition 2, etc.
            // Adjust based on your panel's zone/partition configuration
            return (byte)Math.Max(1, (zoneNumber - 1) / 64 + 1);
        }

        #endregion
    }
}