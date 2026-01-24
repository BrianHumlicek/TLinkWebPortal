using Microsoft.Extensions.Options;
using System.Text.Json;

namespace TLinkWebPortal.Services.Settings
{
    /// <summary>
    /// Generic service for reading and persisting settings to userSettings.json
    /// </summary>
    public interface ISettingsPersistenceService
    {
        object GetSettings(Type settingsType);
        Task SaveSettingsAsync(Type settingsType, object settings);
    }

    public class SettingsPersistenceService : ISettingsPersistenceService
    {
        private const string SettingsFileName = "userSettings.json";
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<SettingsPersistenceService> _log;
        private readonly string _settingsPath;
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        public SettingsPersistenceService(
            IServiceProvider serviceProvider,
            IWebHostEnvironment env,
            ILogger<SettingsPersistenceService> log)
        {
            _serviceProvider = serviceProvider;
            _env = env;
            _log = log;
            _settingsPath = Path.Combine(_env.ContentRootPath, SettingsFileName);
        }

        public object GetSettings(Type settingsType)
        {
            // Use IOptionsMonitor to get current configuration values
            var optionsMonitorType = typeof(IOptionsMonitor<>).MakeGenericType(settingsType);
            var optionsMonitor = _serviceProvider.GetRequiredService(optionsMonitorType);
            var currentValueProperty = optionsMonitorType.GetProperty("CurrentValue");
            return currentValueProperty!.GetValue(optionsMonitor)!;
        }

        public async Task SaveSettingsAsync(Type settingsType, object settings)
        {
            // Find section name from metadata
            var sectionNameField = settingsType.GetField("SectionName", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var sectionName = sectionNameField?.GetValue(null)?.ToString() ?? settingsType.Name;

            await _fileLock.WaitAsync();
            try
            {
                // Read existing file or create new
                Dictionary<string, object>? rootSettings = null;
                if (File.Exists(_settingsPath))
                {
                    var json = await File.ReadAllTextAsync(_settingsPath);
                    rootSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }
                rootSettings ??= new Dictionary<string, object>();

                // Update the specific section
                rootSettings[sectionName] = settings;

                // Write back with formatting
                var options = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonSerializer.Serialize(rootSettings, options);
                await File.WriteAllTextAsync(_settingsPath, updatedJson);

                _log.LogInformation("Saved settings for section {Section}", sectionName);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}