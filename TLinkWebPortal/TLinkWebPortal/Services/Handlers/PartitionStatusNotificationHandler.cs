using DSC.TLink.ITv2.MediatR;
using DSC.TLink.ITv2.Messages;
using MediatR;
using TLinkWebPortal.Services.Models;
using static DSC.TLink.ITv2.Messages.NotificationPartitionReadyStatus;

namespace TLinkWebPortal.Services.Handlers
{
    /// <summary>
    /// Handles partition ready-status notifications and updates the state service.
    /// </summary>
    public class PartitionStatusNotificationHandler 
        : INotificationHandler<SessionNotification<NotificationPartitionReadyStatus>>
    {
        private readonly IPartitionStatusService _service;
        private readonly ILogger<PartitionStatusNotificationHandler> _logger;

        public PartitionStatusNotificationHandler(
            IPartitionStatusService service,
            ILogger<PartitionStatusNotificationHandler> logger)
        {
            _service = service;
            _logger = logger;
        }

        public Task Handle(
            SessionNotification<NotificationPartitionReadyStatus> notification,
            CancellationToken cancellationToken)
        {
            var msg = notification.MessageData;
            var sessionId = notification.SessionId;

            _logger.LogInformation(
                "Partition {Partition} status: {Status} (Session: {SessionId})",
                msg.PartitionNumber, msg.Status, sessionId);

            var partition = _service.GetPartition(sessionId, msg.PartitionNumber) 
                ?? new PartitionState { PartitionNumber = msg.PartitionNumber };
            
            partition.IsReady = msg.Status is PartitionReadyStatusEnum.ReadyToArm 
                                            or PartitionReadyStatusEnum.ReadyToForceArm;
            
            partition.ArmMode = msg.Status switch
            {
                PartitionReadyStatusEnum.ReadyToArm => "Ready",
                PartitionReadyStatusEnum.ReadyToForceArm => "Ready (Force)",
                PartitionReadyStatusEnum.NotReadyToArm => "Not Ready",
                _ => "Unknown"
            };
            
            partition.LastUpdated = notification.ReceivedAt;

            _service.UpdatePartition(sessionId, partition);

            return Task.CompletedTask;
        }
    }
}