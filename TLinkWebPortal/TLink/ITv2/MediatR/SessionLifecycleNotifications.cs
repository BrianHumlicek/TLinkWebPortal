using MediatR;

namespace DSC.TLink.ITv2.MediatR
{
    public record SessionConnectedNotification(string SessionId) : INotification;
    public record SessionDisconnectedNotification(string SessionId) : INotification;
}