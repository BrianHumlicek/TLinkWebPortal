using Microsoft.Extensions.Options;

namespace TLinkWebPortal.Services.Diagnostics
{
    /// <summary>
    /// Custom logger provider that feeds logs into the diagnostics service.
    /// Supports per-category log level overrides and app-only filtering.
    /// </summary>
    public class DiagnosticsLoggerProvider : ILoggerProvider
    {
        private static readonly string[] SolutionPrefixes = ["TLinkWebPortal", "DSC.TLink"];

        private readonly IDiagnosticsLogService _diagnosticsService;
        private readonly IOptionsMonitor<DiagnosticsSettings> _settings;

        public DiagnosticsLoggerProvider(
            IDiagnosticsLogService diagnosticsService,
            IOptionsMonitor<DiagnosticsSettings> settings)
        {
            _diagnosticsService = diagnosticsService;
            _settings = settings;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new DiagnosticsLogger(categoryName, _diagnosticsService, _settings);
        }

        public void Dispose() { }

        private class DiagnosticsLogger : ILogger
        {
            private readonly string _category;
            private readonly IDiagnosticsLogService _diagnosticsService;
            private readonly IOptionsMonitor<DiagnosticsSettings> _settings;

            public DiagnosticsLogger(
                string category,
                IDiagnosticsLogService diagnosticsService,
                IOptionsMonitor<DiagnosticsSettings> settings)
            {
                _category = category;
                _diagnosticsService = diagnosticsService;
                _settings = settings;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel)
            {
                var settings = _settings.CurrentValue;

                // App-only filter: skip non-solution categories entirely
                if (settings.AppOnly && !IsSolutionCategory(_category))
                    return false;

                // Per-category override takes precedence
                if (settings.CategoryOverrides.TryGetValue(_category, out var categoryLevel))
                    return logLevel >= categoryLevel;

                // Fall back to global minimum
                return logLevel >= settings.MinimumLogLevel;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                _diagnosticsService.AddLog(new DiagnosticsLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    LogLevel = logLevel,
                    Category = _category,
                    Message = formatter(state, exception),
                    Exception = exception
                });
            }

            private static bool IsSolutionCategory(string category)
            {
                return SolutionPrefixes.Any(p => category.StartsWith(p, StringComparison.Ordinal));
            }
        }
    }
}