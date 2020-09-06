using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNetConfig;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    public class LocalSettings
    {
        /// <summary>
        /// Entire sleet.json file.
        /// </summary>
        public JObject Json { get; set; } = new JObject();

        /// <summary>
        /// Absolute path of the sleet.json file
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Feed lock wait time.
        /// config/feedLockTimeoutMinutes
        /// </summary>
        public TimeSpan FeedLockTimeout { get; set; } = TimeSpan.MaxValue;

        /// <summary>
        /// Message written to feedback. This will be shown to waiting clients.
        /// </summary>
        public string FeedLockMessage { get; set; }

        public static LocalSettings Load(JObject json)
        {
            return Load(json, null);
        }

        public static LocalSettings Load(JObject json, string path)
        {
            return new LocalSettings()
            {
                Json = json,
                Path = path,
                FeedLockTimeout = GetFeedLockTimeout(json)
            };
        }

        public static LocalSettings Load(string path, Dictionary<string, string> mappings)
        {
            JObject json = null;

            // Look up the file or search parent directories
            // None is a special keyword to skip config resolution
            var skipConfig = StringComparer.OrdinalIgnoreCase.Equals(path, "none");

            if (!skipConfig)
            {
                var resolvedPath = SettingsUtility.GetSleetJsonPathOrNull(path);

                if (resolvedPath != null && !resolvedPath.EndsWith(".netconfig", StringComparison.Ordinal))
                {
                    json = JObject.Parse(File.ReadAllText(resolvedPath));

                    // Resolve tokens in the json
                    SettingsUtility.ResolveTokensInSettingsJson(json, mappings);

                    return Load(json, resolvedPath);
                }
                else if ((path == null || path.EndsWith(".netconfig", StringComparison.Ordinal)) &&
                    Config.Build(path) is Config config && config.GetRegex("sleet").Any())
                {
                    // Use .netconfig only if at least one sleet setting is found.

                    json = new JObject(new JProperty("config", new JObject()));
                    if (config.TryGetString("sleet", "username", out var username))
                        json["username"] = username;
                    if (config.TryGetString("sleet", "useremail", out var useremail))
                        json["useremail"] = useremail;
                    if (config.TryGetNumber("sleet", "feedLockTimeoutMinutes", out var feedLockTimeoutMinutes))
                        json["config"]["feedLockTimeoutMinutes"] = feedLockTimeoutMinutes;
                    if (config.TryGetBoolean("sleet", "proxy-useDefaultCredentials", out var useDefaultCredentials))
                        json["proxy"] = new JObject(new JProperty("useDefaultCredentials", useDefaultCredentials));

                    var sources = new JArray();
                    json.Add("sources", sources);

                    foreach (var properties in config
                        .Where(x => x.Section == "sleet" && x.Subsection != null)
                        .GroupBy(x => x.Subsection))
                    {
                        var source = new JObject(properties.Select(x => new JProperty(x.Variable, x.RawValue ?? (object)true)));
                        source.AddFirst(new JProperty("name", properties.Key));
                        sources.Add(source);
                    }

                    // Resolve tokens in the json
                    SettingsUtility.ResolveTokensInSettingsJson(json, mappings);

                    return Load(json, config.FilePath);
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    if (!File.Exists(path))
                        // A path was given but was not found, throw
                        throw new FileNotFoundException($"Unable to find source settings. File not found '{path}'.");
                    else
                        // A path was given but it has no sleet settings
                        throw new ArgumentException($"Unable to find source settings in '{path}'.", nameof(path));
                }
            }

            // Read from env vars
            json = SettingsUtility.GetConfigFromEnv(mappings);

            if (json != null)
            {
                return Load(json);
            }

            throw new InvalidOperationException($"Unable to find source settings. Specify the path to a sleet.json settings file.");
        }

        public static LocalSettings Load(string path)
        {
            return Load(path, mappings: null);
        }

        internal static TimeSpan GetFeedLockTimeout(JObject json)
        {
            var timeout = TimeSpan.MaxValue;

            var config = GetGlobalConfig(json);

            var s = config["feedLockTimeoutMinutes"]?.ToObject<string>();

            if (!string.IsNullOrEmpty(s))
            {
                timeout = TimeSpan.FromMinutes(int.Parse(s));
            }

            return timeout;
        }

        private static JObject GetGlobalConfig(JObject json)
        {
            return json["config"] as JObject ?? new JObject();
        }

        internal static JObject GetSourceSettings(LocalSettings settings, string sourceName)
        {
            var sources = settings.Json["sources"] as JArray;

            if (sources != null)
            {
                foreach (var sourceEntry in sources)
                {
                    var name = sourceEntry["name"]?.ToObject<string>();

                    if (StringComparer.OrdinalIgnoreCase.Equals(sourceName, name))
                    {
                        return sourceEntry as JObject;
                    }
                }
            }

            return new JObject();
        }


    }
}