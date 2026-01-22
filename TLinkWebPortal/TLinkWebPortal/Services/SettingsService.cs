using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TLinkWebPortal.Services
{
    public sealed class SettingsService
    {
        public const int DefaultServerPort = 3072;

        public const string TLinkSettingsFilename = "TLinkSettings.json";
        public const string TLinkSettingsSectionName = "TLinkSettings";

        private readonly IOptionsMonitor<TLinkSettings> _optionsMonitor;
        private readonly IHostEnvironment _env;
        private readonly object _sync = new();

        /// <summary>
        /// Expose current settings via IOptionsMonitor so consumers get the framework-standard behavior
        /// and respond to reloadOnChange.
        /// </summary>
        public TLinkSettings Settings => _optionsMonitor.CurrentValue;

        public SettingsService(IOptionsMonitor<TLinkSettings> optionsMonitor, IHostEnvironment env)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        /// <summary>
        /// Persist updated settings to the runtime JSON file (userSettings.json).
        /// Uses the application's content root as the storage location and writes atomically.
        /// </summary>
        public void UpdateAndPersist(TLinkSettings s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            lock (_sync)
            {
                PersistToFile(s);
            }
        }

        /// <summary>
        /// Persist the provided settings into userSettings.json under the "TLinkSettings" node.
        /// If the file exists other sections are preserved.
        /// </summary>
        private void PersistToFile(TLinkSettings s)
        {
            var path = Path.Combine(_env.ContentRootPath, TLinkSettingsFilename);

            JsonObject root;

            if (File.Exists(path))
            {
                try
                {
                    var text = File.ReadAllText(path);
                    var parsed = JsonNode.Parse(text);
                    root = parsed?.AsObject() ?? new JsonObject();
                }
                catch
                {
                    // If parse fails, start from a fresh object to avoid corrupt file issues.
                    root = new JsonObject();
                }
            }
            else
            {
                root = new JsonObject();
            }

            // Serialize provided settings into the "TLinkSettings" property
            var options = new JsonSerializerOptions { WriteIndented = true };
            root[TLinkSettingsSectionName] = JsonSerializer.SerializeToNode(s, options);

            // Write atomically
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, root.ToJsonString(options));
            File.Copy(tmp, path, overwrite: true);
            File.Delete(tmp);
        }
    }
    public static class IConfigurationExtensions
    {
        public static int GetServerPort(this IConfiguration configuration)
        {
            return configuration.GetValue<int>($"{SettingsService.TLinkSettingsSectionName}:{nameof(TLinkSettings.ServerPort)}", SettingsService.DefaultServerPort);
        }
    }

    public sealed class TLinkSettings
    {
        [Description("Integration Notification port [851][429]")]
        public int ServerPort { get; set; } = SettingsService.DefaultServerPort;
        [Description("Type 1 Integration Access Code [851][423,450,477,504]")]
        public string IntegrationAccessCodeType1 { get; set; } = "12345678";
        [Description("Type 2 Integration Access Code [851][700,701,702,703]")]
        public string IntegrationAccessCodeType2 { get; set; } = "23456789";
        [Description("Integration Identification Number [851][422]")]
        public string IntegrationIdentificationNumber { get; set; } = "87654321";

    }
}