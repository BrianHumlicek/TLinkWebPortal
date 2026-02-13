using System.ComponentModel.DataAnnotations;

namespace TLinkWebPortal.Services.Diagnostics
{
    [Display(Name = "Diagnostics", Description = "Log viewer and diagnostics settings")]
    public class DiagnosticsSettings
    {
        public const string SectionName = "Diagnostics";

        [Display(
            Name = "Minimum Log Level",
            Description = "Minimum log level to display in diagnostics viewer",
            Order = 1)]
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    }
}