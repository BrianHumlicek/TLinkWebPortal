using System.ComponentModel.DataAnnotations;

namespace TLinkWebPortal.Services.Settings
{
    /// <summary>
    /// Metadata about a discovered settings section
    /// </summary>
    public record SettingsSectionMetadata
    {
        public Type SettingsType { get; init; } = null!;
        public string SectionName { get; init; } = null!;
        public string DisplayName { get; init; } = null!;
        public string? Description { get; init; }
        public string? GroupName { get; init; }
        public List<SettingsPropertyMetadata> Properties { get; init; } = new();
    }

    /// <summary>
    /// Metadata about a settings property extracted from Display attribute and reflection
    /// </summary>
    public record SettingsPropertyMetadata
    {
        public string PropertyName { get; init; } = null!;
        public string DisplayName { get; init; } = null!;
        public string? Description { get; init; }
        public string? GroupName { get; init; }
        public Type PropertyType { get; init; } = null!;
        public int Order { get; init; }
        public object? DefaultValue { get; init; }
        public bool IsRequired { get; init; }
        public bool IsSensitive { get; init; }
    }
}