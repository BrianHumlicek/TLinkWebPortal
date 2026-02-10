using DSC.TLink.ITv2.Messages;
using DSC.TLink.ITv2.Transactions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DSC.TLink.ITv2.MediatR
{
    /// <summary>
    /// Unified mediator that handles both outbound commands (from Blazor UI) 
    /// and publishes inbound notifications (from panel).
    /// Registered as singleton - uses SessionManager for routing.
    /// </summary>
    internal class SessionMediator : IRequestHandler<SessionCommand, SessionResponse>
    {
        private readonly IMediator _mediator;
        private readonly IITv2SessionManager _sessionManager;
        private readonly ILogger<SessionMediator> _logger;

        public SessionMediator(
            IMediator mediator,
            IITv2SessionManager sessionManager,
            ILogger<SessionMediator> logger)
        {
            _mediator = mediator;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        #region Command Handling (Outbound from Blazor UI)

        /// <summary>
        /// Handles commands from Blazor UI by routing to the appropriate session.
        /// </summary>
        public async Task<SessionResponse> Handle(
            SessionCommand request,
            CancellationToken cancellationToken)
        {
            var session = _sessionManager.GetSession(request.SessionID);
            if (session == null)
            {
                _logger.LogWarning("Command failed - session {SessionId} not found", request.SessionID);
                return new SessionResponse
                {
                    Success = false,
                    ErrorMessage = $"Session {request.SessionID} not found"
                };
            }

            try
            {
                var result = await session.SendMessageAsync(request.MessageData, cancellationToken);
                return new SessionResponse
                {
                    Success = result.Success,
                    MessageData = result.MessageData as IMessageData,
                    ErrorMessage = result.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling command for session {SessionId}", request.SessionID);
                return new SessionResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

        #region Notification Publishing (Inbound from Panel)

        /// <summary>
        /// Publishes inbound transaction results as generic SessionNotification&lt;T&gt;.
        /// Called by ITv2Session for each received message.
        /// </summary>
        /// <param name="sessionId">The session ID that received the message</param>
        /// <param name="transactionResultTask">The transaction result task</param>
        public async void PublishInboundMessage(string sessionId, Task<TransactionResult> transactionResultTask)
        {
            try
            {
                var result = await transactionResultTask;

                if (!result.Success)
                {
                    _logger.LogWarning("Transaction failed for session {SessionId}: {Error}",
                        sessionId, result.ErrorMessage);
                    return;
                }

                if (result.MessageData is MultipleMessagePacket multiMessage)
                {
                    _logger.LogTrace("Received multi message packet with {Count} messages for session {SessionId}",
                        multiMessage.Messages.Length, sessionId);

                    foreach (var messageData in multiMessage.Messages)
                    {
                        await PublishGenericNotification(sessionId, messageData);
                        _logger.LogTrace("MULTI PART: Published SessionNotification<{MessageType}> for session {SessionId}",
                            messageData.GetType().Name, sessionId);
                    }
                    return;
                }

                await PublishGenericNotification(sessionId, result.MessageData);
                _logger.LogTrace("Published SessionNotification<{MessageType}> for session {SessionId}",
                        result.MessageData.GetType().Name, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing notification for session {SessionId}", sessionId);
            }
        }

        private async Task PublishGenericNotification(string sessionId, IMessageData messageData)
        {
            // Use reflection to create SessionNotification<T> with the concrete message type
            var messageType = messageData.GetType();
            var notificationType = typeof(SessionNotification<>).MakeGenericType(messageType);

            // Create the notification instance
            var notification = Activator.CreateInstance(
                notificationType,
                sessionId,
                messageData,
                DateTime.UtcNow);

            if (notification == null)
            {
                _logger.LogError("Failed to create notification for type {MessageType}", messageType.Name);
                return;
            }

            // Publish it through MediatR
            await _mediator.Publish(notification);
        }

        #endregion
    }
}
