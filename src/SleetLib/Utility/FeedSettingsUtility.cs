using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Read/Write sleet.settings.json
    /// </summary>
    public static class FeedSettingsUtility
    {
        /// <summary>
        /// Read file and load it into FeedSettings.
        /// </summary>
        public static async Task<FeedSettings> GetSettingsOrDefault(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var file = GetSettingsFileFromFeed(fileSystem);

            var json = await file.GetJsonOrNull(log, token);

            if (json != null)
            {
                log.LogDebug("Found settings");

                return LoadSettings(GetSettings(json));
            }
            else
            {
                log.LogDebug("Unable to find settings");

                return new FeedSettings();
            }
        }

        public static async Task SaveSettings(ISleetFileSystem fileSystem, FeedSettings settings, ILogger log, CancellationToken token)
        {
            // Get current file
            var file = GetSettingsFileFromFeed(fileSystem);

            // Update json
            var json = await file.GetJson(log, token);
            var values = LoadSettings(settings);
            Set(json, values);

            // Save
            await file.Write(json, log, token);
        }

        /// <summary>
        /// Load settings from a dictionary.
        /// </summary>
        public static FeedSettings LoadSettings(IDictionary<string, string> values)
        {
            var settings = new FeedSettings();

            foreach (var pair in values)
            {
                switch (pair.Key.ToLowerInvariant())
                {
                    case "catalogenabled":
                        settings.CatalogEnabled = GetBoolOrDefault(pair.Value, defaultValue: false);
                        break;
                    case "catalogpagesize":
                        settings.CatalogPageSize = Math.Max(1, GetIntOrDefault(pair.Value, defaultValue: 1024));
                        break;
                    case "symbolsfeedenabled":
                        settings.SymbolsEnabled = GetBoolOrDefault(pair.Value, defaultValue: false);
                        break;
                }
            }

            return settings;
        }

        /// <summary>
        /// Load settings from a dictionary.
        /// </summary>
        public static IDictionary<string, string> LoadSettings(FeedSettings settings)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "catalogenabled", settings.CatalogEnabled.ToString().ToLowerInvariant() },
                { "catalogpagesize", settings.CatalogPageSize.ToString() },
                { "symbolsfeedenabled", settings.SymbolsEnabled.ToString().ToLowerInvariant() }
            };
            return values;
        }

        private static bool GetBoolOrDefault(string s, bool defaultValue)
        {
            switch (s?.ToLowerInvariant())
            {
                case "true":
                    return true;
                case "false":
                    return false;
            }

            return defaultValue;
        }

        private static int GetIntOrDefault(string s, int defaultValue)
        {
            if (int.TryParse(s, out var result))
            {
                return result;
            }

            return defaultValue;
        }

        /// <summary>
        /// Get sleet.settings.json from a feed.
        /// </summary>
        public static ISleetFile GetSettingsFileFromFeed(ISleetFileSystem fileSystem)
        {
            return fileSystem.Get("sleet.settings.json");
        }

        /// <summary>
        /// Read all settings.
        /// </summary>
        public static IDictionary<string, string> GetSettings(JObject settingsJson)
        {
            var array = settingsJson["feedSettings"] as JArray ?? new JArray();

            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in array.Select(e => ParseSettingEntry((JObject)e)))
            {
                if (!string.IsNullOrEmpty(entry.Value))
                {
                    settings.Add(entry.Key, entry.Value);
                }
            }

            return settings;
        }

        /// <summary>
        /// Clear settings.
        /// </summary>
        public static void UnsetAll(JObject settingsJson)
        {
            settingsJson["feedSettings"] = new JArray();
        }

        /// <summary>
        /// Write settings.
        /// </summary>
        public static void Set(JObject settingsJson, IDictionary<string, string> settings)
        {
            var id = JsonUtility.GetIdUri(settingsJson);

            settingsJson["feedSettings"] = new JArray(
                settings.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                        .Where(e => !string.IsNullOrEmpty(e.Key) && !string.IsNullOrEmpty(e.Value))
                        .Select(e => CreateSettingEntry(id, e)));

            settingsJson["lastEdited"] = DateTimeOffset.UtcNow.GetDateString();
        }

        private static KeyValuePair<string, string> ParseSettingEntry(JObject entry)
        {
            return new KeyValuePair<string, string>(entry["key"].ToObject<string>(), entry["value"].ToObject<string>());
        }

        private static JObject CreateSettingEntry(Uri settingsUri, KeyValuePair<string, string> setting)
        {
            var json = new JObject();

            var lowerKey = setting.Key.ToLowerInvariant();

            json["@id"] = JsonUtility.GetId(settingsUri, lowerKey);
            json["@type"] = "FeedSetting";
            json["key"] = lowerKey;
            json["value"] = setting.Value;

            return json;
        }
    }
}
