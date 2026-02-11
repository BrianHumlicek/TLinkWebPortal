using DSC.TLink.ITv2.MediatR;
using DSC.TLink.ITv2.Messages;
using MediatR;
using TLinkWebPortal.Services.Models;

namespace TLinkWebPortal.Services.Handlers
{
    /// <summary>
    /// Handles lifestyle zone status notifications (real-time open/close events).
    /// </summary>
    public class ZoneStatusNotificationHandler 
        : INotificationHandler<SessionNotification<NotificationLifestyleZoneStatus>>
    {
        private readonly IPartitionStatusService _service;
        private readonly ILogger<ZoneStatusNotificationHandler> _logger;

        public ZoneStatusNotificationHandler(
            IPartitionStatusService service,
            ILogger<ZoneStatusNotificationHandler> logger)
        {
            _service = service;
            _logger = logger;
        }

        public Task Handle(
            SessionNotification<NotificationLifestyleZoneStatus> notification,
            CancellationToken cancellationToken)
        {
            var msg = notification.MessageData;
            var sessionId = notification.SessionId;

            // Zones 1-64 = partition 1, 65-128 = partition 2, etc.
            byte partitionNumber = (byte)Math.Max(1, (msg.ZoneNumber - 1) / 64 + 1);

            var zone = _service.GetZone(sessionId, partitionNumber, msg.ZoneNumber) 
                ?? new ZoneState { ZoneNumber = msg.ZoneNumber };

            zone.IsOpen = msg.Status == NotificationLifestyleZoneStatus.LifeStyleZoneStatusCode.Open;
            zone.LastUpdated = notification.ReceivedAt;

            _logger.LogDebug(
                "Zone {Zone} is now {Status} (Partition: {Partition}, Session: {SessionId})",
                msg.ZoneNumber, zone.IsOpen ? "OPEN" : "CLOSED", partitionNumber, sessionId);

            _service.UpdateZone(sessionId, partitionNumber, zone);

            return Task.CompletedTask;
        }
    }
}