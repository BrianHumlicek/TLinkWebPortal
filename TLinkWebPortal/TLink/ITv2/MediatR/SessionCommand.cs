using DSC.TLink.ITv2.Messages;
using MediatR;

namespace DSC.TLink.ITv2.MediatR
{
    /// <summary>
    /// Command to send a message to a specific panel session.
    /// Used by Blazor UI to interact with the panel.
    /// </summary>
    public record SessionCommand : IRequest<SessionResponse>
    {
        public required string SessionID { get; init; }
        public required IMessageData MessageData { get; init; }
    }
}
