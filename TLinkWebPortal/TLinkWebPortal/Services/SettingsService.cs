//using System;
//using System.ComponentModel;
//using System.IO;
//using System.Text.Json;
//using System.Text.Json.Nodes;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Options;
//using System.Threading.Tasks;
//using System.Collections.Generic;
//using Microsoft.Extensions.Logging;

//namespace TLinkWebPortal.Services
//{
//    public class SettingsService
//    {
//        public const string TLinkSettingsFilename = "tlinkSettings.json";
//        public const string TLinkSettingsSectionName = "TLink";

//        private readonly IOptionsMonitor<TLinkSettings> _options;
//        private readonly IWebHostEnvironment _env;
//        private readonly ILogger<SettingsService> _log;
//        private readonly string _settingsPath;
//        private readonly SemaphoreSlim _fileLock = new(1, 1);

//        public SettingsService(
//            IOptionsMonitor<TLinkSettings> options,
//            IWebHostEnvironment env,
//            ILogger<SettingsService> log)
//        {
//            _options = options;
//            _env = env;
//            _log = log;
//            _settingsPath = Path.Combine(_env.ContentRootPath, TLinkSettingsFilename);
//        }

//        public TLinkSettings GetSettings() => _options.CurrentValue;

//        public async Task UpdateSettingsAsync(TLinkSettings settings)
//        {
//            await _fileLock.WaitAsync();
//            try
//            {
//                Dictionary<string, object>? rootSettings = null;

//                if (File.Exists(_settingsPath))
//                {
//                    var json = await File.ReadAllTextAsync(_settingsPath);
//                    rootSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
//                }

//                rootSettings ??= new Dictionary<string, object>();
//                rootSettings[TLinkSettingsSectionName] = settings;

//                var options = new JsonSerializerOptions
//                {
//                    WriteIndented = true
//                };

//                var updatedJson = JsonSerializer.Serialize(rootSettings, options);
//                await File.WriteAllTextAsync(_settingsPath, updatedJson);

//                _log.LogInformation("Updated TLink settings in {File}", TLinkSettingsFilename);
//            }
//            finally
//            {
//                _fileLock.Release();
//            }
//        }
//    }
//    public static class IConfigurationExtensions
//    {
//        public static int GetServerPort(this IConfiguration configuration)
//        {
//            return configuration.GetValue<int>($"{SettingsService.TLinkSettingsSectionName}:{nameof(TLinkSettings.ServerPort)}", SettingsService.DefaultServerPort);
//        }
//    }

//    public sealed class TLinkSettings
//    {
//        [Description("Integration Notification port [851][429]")]
//        public int ServerPort { get; set; } = SettingsService.DefaultServerPort;
//        [Description("Type 1 Integration Access Code [851][423,450,477,504]")]
//        public string IntegrationAccessCodeType1 { get; set; } = "12345678";
//        [Description("Type 2 Integration Access Code [851][700,701,702,703]")]
//        public string IntegrationAccessCodeType2 { get; set; } = "23456789";
//        [Description("Integration Identification Number [851][422]")]
//        public string IntegrationIdentificationNumber { get; set; } = "87654321";

//    }
//}