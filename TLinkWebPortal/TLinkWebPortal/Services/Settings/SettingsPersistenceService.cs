using Microsoft.Extensions.Options;
using System.Text.Json;

namespace TLinkWebPortal.Services.Settings
{
    /// <summary>
    /// Generic service for reading and persisting settings to persist/userSettings.json
    /// </summary>
    public interface ISettingsPersistenceService
    {
        object GetSettings(Type settingsType);
        Task SaveSettingsAsync(Type settingsType, object settings);
        string PersistPath { get; }
    }

    public class SettingsPersistenceService : ISettingsPersistenceService
    {
        private const string PersistFolder = "persist";
        private const string SettingsFileName = "userSettings.json";
        
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SettingsPersistenceService> _log;
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        /// <summary>
        /// Returns the relative path to userSettings.json (relative to ContentRootPath).
        /// Used by both Program.cs (AddJsonFile) and this service (read/write).
        /// </summary>
        public static string SettingsFileRelativePath => Path.Combine(PersistFolder, SettingsFileName);

        public string PersistPath { get; }
        private string SettingsFilePath { get; }

        public SettingsPersistenceService(
            IServiceProvider serviceProvider,
            IWebHostEnvironment env,
            ILogger<SettingsPersistenceService> log)
        {
            _serviceProvider = serviceProvider;
            _log = log;
            PersistPath = Path.Combine(env.ContentRootPath, PersistFolder);
            SettingsFilePath = Path.Combine(env.ContentRootPath, SettingsFileRelativePath);

            // Ensure persist directory exists
            Directory.CreateDirectory(PersistPath);
            _log.LogInformation("Settings file: {Path}", SettingsFilePath);
        }

        public object GetSettings(Type settingsType)
        {
            var optionsMonitorType = typeof(IOptionsMonitor<>).MakeGenericType(settingsType);
            var optionsMonitor = _serviceProvider.GetRequiredService(optionsMonitorType);
            var currentValueProperty = optionsMonitorType.GetProperty("CurrentValue");
            return currentValueProperty!.GetValue(optionsMonitor)!;
        }

        public async Task SaveSettingsAsync(Type settingsType, object settings)
        {
            var sectionNameField = settingsType.GetField("SectionName", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var sectionName = sectionNameField?.GetValue(null)?.ToString() ?? settingsType.Name;

            await _fileLock.WaitAsync();
            try
            {
                var rootSettings = await ReadSettingsFileAsync();
                rootSettings[sectionName] = settings;

                var options = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonSerializer.Serialize(rootSettings, options);
                await File.WriteAllTextAsync(SettingsFilePath, updatedJson);

                _log.LogInformation("Saved settings for section {Section}", sectionName);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task<Dictionary<string, object>> ReadSettingsFileAsync()
        {
            if (!File.Exists(SettingsFilePath))
                return new Dictionary<string, object>();

            try
            {
                var json = await File.ReadAllTextAsync(SettingsFilePath);
                
                if (string.IsNullOrWhiteSpace(json))
                    return new Dictionary<string, object>();

                return JsonSerializer.Deserialize<Dictionary<string, object>>(json) 
                    ?? new Dictionary<string, object>();
            }
            catch (JsonException ex)
            {
                _log.LogWarning(ex, "Invalid JSON in {File}, starting fresh", SettingsFilePath);
                return new Dictionary<string, object>();
            }
        }
    }
}