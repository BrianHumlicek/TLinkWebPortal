using Microsoft.Extensions.Options;

namespace TLinkWebPortal.Services.Diagnostics
{
    /// <summary>
    /// Custom logger provider that feeds logs into the diagnostics service.
    /// </summary>
    public class DiagnosticsLoggerProvider : ILoggerProvider
    {
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
                return logLevel >= _settings.CurrentValue.MinimumLogLevel;
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
        }
    }
}