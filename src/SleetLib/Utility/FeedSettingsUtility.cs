using System;
using System.Collections.Generic;
using System.Globalization;
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
        private static readonly char[] KeyValueSeparator = [':'];

        // Keys read by LoadSettings, grouped by the type of value they take.
        private static readonly string[] BoolSettings = ["badgesenabled", "catalogenabled", "galleryenabled"];
        private static readonly string[] IntSettings = ["catalogpagesize", "retentiongroupbyfirstprereleaselabelcount", "retentionmaxprereleaseversions", "retentionmaxstableversions"];
        private static readonly string[] StringSettings = ["externalsearch"];

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
                    case "badgesenabled":
                        settings.BadgesEnabled = GetBoolOrDefault(pair.Value, defaultValue: true);
                        break;
                    case "galleryenabled":
                        settings.GalleryEnabled = GetBoolOrDefault(pair.Value, defaultValue: true);
                        break;
                    case "retentionmaxstableversions":
                        settings.RetentionMaxStableVersions = GetIntOrDefault(pair.Value, defaultValue: -1);
                        break;
                    case "retentionmaxprereleaseversions":
                        settings.RetentionMaxPrereleaseVersions = GetIntOrDefault(pair.Value, defaultValue: -1);
                        break;
                    case "retentiongroupbyfirstprereleaselabelcount":
                        settings.RetentionGroupByFirstPrereleaseLabelCount = GetIntOrDefault(pair.Value, defaultValue: -1);
                        break;
                    case "externalsearch":
                        settings.ExternalSearch = GetStringOrDefault(pair.Value, null);
                        break;
                }
            }

            if (settings.RetentionMaxStableVersions < 1)
            {
                settings.RetentionMaxStableVersions = null;
            }

            if (settings.RetentionMaxPrereleaseVersions < 1)
            {
                settings.RetentionMaxPrereleaseVersions = null;
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
                { "catalogpagesize", settings.CatalogPageSize.ToString(CultureInfo.InvariantCulture) },
                { "badgesenabled", settings.BadgesEnabled.ToString().ToLowerInvariant() },
                { "galleryenabled", settings.GalleryEnabled.ToString().ToLowerInvariant() }
            };

            if (settings.RetentionMaxStableVersions > 0)
            {
                values.Add("retentionmaxstableversions", settings.RetentionMaxStableVersions.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (settings.RetentionMaxPrereleaseVersions > 0)
            {
                values.Add("retentionmaxprereleaseversions", settings.RetentionMaxPrereleaseVersions.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (settings.RetentionGroupByFirstPrereleaseLabelCount > 0)
            {
                values.Add("retentiongroupbyfirstprereleaselabelcount", settings.RetentionGroupByFirstPrereleaseLabelCount.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(settings.ExternalSearch))
            {
                values.Add("externalsearch", settings.ExternalSearch);
            }

            return values;
        }

        /// <summary>
        /// Parse settings in the form {key}:{value}. Keys are lowercased, values may contain ':'.
        /// Identical inputs are returned once, a key given more than one value throws.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> ParseSettingValues(IEnumerable<string> inputs)
        {
            var values = new List<KeyValuePair<string, string>>();
            var keysSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var input in new SortedSet<string>(inputs, StringComparer.OrdinalIgnoreCase))
            {
                var parts = input.Split(KeyValueSeparator, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2 || string.IsNullOrEmpty(parts[0]?.Trim()) || string.IsNullOrEmpty(parts[1]?.Trim()))
                {
                    throw new ArgumentException("Value must be in the form {key}:{value}. Invalid: '" + input + "'");
                }

                var key = parts[0].Trim().ToLowerInvariant();

                // Allow : in the value, not the key
                var value = input.Substring(key.Length + 1);

                if (!keysSeen.Add(key))
                {
                    throw new ArgumentException($"Duplicate values for '{key}'. This value may only be set once.");
                }

                values.Add(new KeyValuePair<string, string>(key, value));
            }

            return values;
        }

        /// <summary>
        /// Parse settings in the form {key}:{value}, and throw if any of them is not a setting Sleet can read.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> ParseAndValidateSettingValues(IEnumerable<string> inputs)
        {
            var values = ParseSettingValues(inputs);

            foreach (var pair in values)
            {
                ValidateSetting(pair.Key, pair.Value);
            }

            return values;
        }

        /// <summary>
        /// Throw if Sleet does not read the key, or would not be able to read the value.
        /// </summary>
        public static void ValidateSetting(string key, string value)
        {
            if (BoolSettings.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                // Same check as GetBoolOrDefault
                var lower = value.ToLowerInvariant();

                if (lower != "true" && lower != "false")
                {
                    throw new ArgumentException($"Invalid value '{value}' for '{key}'. Use true or false.");
                }
            }
            else if (IntSettings.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                // Same check as GetIntOrDefault
                if (!int.TryParse(value, out _))
                {
                    throw new ArgumentException($"Invalid value '{value}' for '{key}'. Use a whole number.");
                }
            }
            else if (!StringSettings.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                var known = BoolSettings.Concat(IntSettings).Concat(StringSettings).OrderBy(e => e, StringComparer.Ordinal);

                throw new ArgumentException($"Unknown feed setting '{key}'. Use one of: {string.Join(", ", known)}");
            }
        }

        /// <summary>
        /// Return a copy of the settings with the values applied.
        /// </summary>
        public static FeedSettings ApplySettings(FeedSettings settings, IEnumerable<KeyValuePair<string, string>> values)
        {
            var merged = LoadSettings(settings);

            foreach (var pair in values)
            {
                merged[pair.Key] = pair.Value;
            }

            return LoadSettings(merged);
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

        private static string? GetStringOrDefault(string s, string? defaultValue)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                return s;
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
            var key = entry["key"]?.ToObject<string>() ?? string.Empty;
            var value = entry["value"]?.ToObject<string>() ?? string.Empty;
            return new KeyValuePair<string, string>(key, value);
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

        public static Dictionary<string, IFeedSettingHandler> GetSettingHandlers(SleetContext context)
        {
            var search = new ExternalSearchHandler(context);

            return new Dictionary<string, IFeedSettingHandler>()
            {
                { search.Name, search }
            };
        }

        /// <summary>
        /// Apply settings that change other feed files. Run after InitAsync, which always points search at the static search files.
        /// Returns false if the changes could not be committed.
        /// </summary>
        public static async Task<bool> ApplySettingHandlersAsync(LocalSettings settings, ISleetFileSystem source, FeedSettings feedSettings, ILogger log, CancellationToken token)
        {
            if (feedSettings.ExternalSearch == null)
            {
                return true;
            }

            log.LogMinimal($"Setting external search url: {feedSettings.ExternalSearch}");

            var context = new SleetContext()
            {
                LocalSettings = settings,
                SourceSettings = feedSettings,
                Log = log,
                Source = source,
                Token = token
            };

            var searchHandler = new ExternalSearchHandler(context);
            await searchHandler.SetValue(feedSettings.ExternalSearch);

            return await source.Commit(log, token);
        }
    }
}
