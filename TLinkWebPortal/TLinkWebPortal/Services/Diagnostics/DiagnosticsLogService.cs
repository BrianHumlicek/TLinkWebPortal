using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace TLinkWebPortal.Services.Diagnostics
{
    /// <summary>
    /// Maintains a circular buffer of log entries for the diagnostics page.
    /// </summary>
    public interface IDiagnosticsLogService
    {
        void AddLog(DiagnosticsLogEntry entry);
        IReadOnlyList<DiagnosticsLogEntry> GetLogs();
        IReadOnlySet<string> GetDiscoveredCategories();
        void Clear();
        event Action<DiagnosticsLogEntry>? LogReceived;
    }

    public class DiagnosticsLogService : IDiagnosticsLogService
    {
        private readonly IOptionsMonitor<DiagnosticsSettings> _settings;
        private readonly ConcurrentQueue<DiagnosticsLogEntry> _logs = new();
        private readonly ConcurrentDictionary<string, byte> _discoveredCategories = new();
        
        public event Action<DiagnosticsLogEntry>? LogReceived;

        public DiagnosticsLogService(IOptionsMonitor<DiagnosticsSettings> settings)
        {
            _settings = settings;
        }

        public void AddLog(DiagnosticsLogEntry entry)
        {
            _discoveredCategories.TryAdd(entry.Category, 0);
            _logs.Enqueue(entry);

            var max = _settings.CurrentValue.MaxLogEntries;
            while (_logs.Count > max)
                _logs.TryDequeue(out _);

            LogReceived?.Invoke(entry);
        }

        public IReadOnlyList<DiagnosticsLogEntry> GetLogs()
        {
            return _logs.ToList();
        }

        public IReadOnlySet<string> GetDiscoveredCategories()
        {
            return _discoveredCategories.Keys.ToHashSet();
        }

        public void Clear()
        {
            _logs.Clear();
        }
    }
}