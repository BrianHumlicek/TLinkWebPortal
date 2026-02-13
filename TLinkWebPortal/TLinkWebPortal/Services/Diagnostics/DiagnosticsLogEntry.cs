namespace TLinkWebPortal.Services.Diagnostics
{
    public record DiagnosticsLogEntry
    {
        public required DateTime Timestamp { get; init; }
        public required LogLevel LogLevel { get; init; }
        public required string Category { get; init; }
        public required string Message { get; init; }
        public Exception? Exception { get; init; }
    }
}