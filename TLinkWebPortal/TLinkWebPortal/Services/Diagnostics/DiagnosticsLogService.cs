using System.Collections.Concurrent;

namespace TLinkWebPortal.Services.Diagnostics
{
    /// <summary>
    /// Maintains a circular buffer of log entries for the diagnostics page.
    /// </summary>
    public interface IDiagnosticsLogService
    {
        void AddLog(DiagnosticsLogEntry entry);
        IReadOnlyList<DiagnosticsLogEntry> GetLogs();
        void Clear();
        event Action<DiagnosticsLogEntry>? LogReceived;
    }

    public class DiagnosticsLogService : IDiagnosticsLogService
    {
        private const int MaxLogEntries = 1000;
        private readonly ConcurrentQueue<DiagnosticsLogEntry> _logs = new();
        
        public event Action<DiagnosticsLogEntry>? LogReceived;

        public void AddLog(DiagnosticsLogEntry entry)
        {
            _logs.Enqueue(entry);

            // Trim to max size
            while (_logs.Count > MaxLogEntries)
                _logs.TryDequeue(out _);

            LogReceived?.Invoke(entry);
        }

        public IReadOnlyList<DiagnosticsLogEntry> GetLogs()
        {
            return _logs.ToList();
        }

        public void Clear()
        {
            _logs.Clear();
        }
    }
}