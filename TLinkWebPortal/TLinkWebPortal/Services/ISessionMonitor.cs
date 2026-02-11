namespace TLinkWebPortal.Services
{
    /// <summary>
    /// Provides session lifecycle events for UI consumption.
    /// Backed by MediatR notifications from the TLink library.
    /// </summary>
    public interface ISessionMonitor
    {
        event Action? SessionsChanged;
        void NotifyChanged();
    }

    public class SessionMonitor : ISessionMonitor
    {
        public event Action? SessionsChanged;
        public void NotifyChanged() => SessionsChanged?.Invoke();
    }
}