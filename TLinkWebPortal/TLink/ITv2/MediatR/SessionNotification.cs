using System;
using DSC.TLink.ITv2.Messages;
using MediatR;

namespace DSC.TLink.ITv2.MediatR
{
    /// <summary>
    /// Generic notification for specific message types from the panel.
    /// Allows type-safe handlers for each message type in Blazor.
    /// </summary>
    public record SessionNotification<T> : INotification where T : IMessageData
    {
        public string SessionId { get; init; }
        public T MessageData { get; init; }
        public DateTime ReceivedAt { get; init; }

        public SessionNotification(string sessionId, T messageData, DateTime receivedAt)
        {
            SessionId = sessionId;
            MessageData = messageData;
            ReceivedAt = receivedAt;
        }
    }
}
