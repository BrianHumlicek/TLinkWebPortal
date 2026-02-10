using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DSC.TLink.ITv2.MediatR
{
    /// <summary>
    /// Manages active ITv2Session instances and routes commands to the correct session.
    /// </summary>
    public  interface IITv2SessionManager  // ✅ Changed from internal to public
    {
        internal void RegisterSession(string sessionId, ITv2Session session);
        internal void UnregisterSession(string sessionId);
        internal ITv2Session? GetSession(string sessionId);
        IEnumerable<string> GetActiveSessions();
    }

    internal class ITv2SessionManager : IITv2SessionManager
    {
        private readonly ConcurrentDictionary<string, ITv2Session> _sessions = new();
        private readonly ILogger<ITv2SessionManager> _logger;

        public ITv2SessionManager(ILogger<ITv2SessionManager> logger)
        {
            _logger = logger;
        }

        public void RegisterSession(string sessionId, ITv2Session session)
        {
            if (_sessions.TryAdd(sessionId, session))
            {
                _logger.LogInformation("Registered session {SessionId}. Active sessions: {Count}",
                    sessionId, _sessions.Count);
            }
            else
            {
                _logger.LogWarning("Session {SessionId} already registered", sessionId);
            }
        }

        public void UnregisterSession(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out _))
            {
                _logger.LogInformation("Unregistered session {SessionId}. Active sessions: {Count}",
                    sessionId, _sessions.Count);
            }
        }

        public ITv2Session? GetSession(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }

        public IEnumerable<string> GetActiveSessions()
        {
            return _sessions.Keys.ToList();
        }
    }
}