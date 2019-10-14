using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGetUriUtility = NuGet.Common.UriUtility;

namespace Sleet
{
    public static class SettingsUtility
    {
        public static readonly string EnvVarPrefix = "SLEET_FEED_";
        public static readonly string EnvVarFeedType = "SLEET_FEED_TYPE";

        public static Dictionary<string, string> GetPropertyMappings(List<string> options)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (options != null)
            {
                foreach (var pair in options.Select(ParseProperty))
                {
                    if (!result.ContainsKey(pair.Key))
                    {
                        result.Add(pair.Key, pair.Value);
                    }
                }
            }

            return result;
        }

        public static KeyValuePair<string, string> ParseProperty(string input)
        {
            var parts = input.Split('=');

            if (parts.Length < 2)
            {
                throw new ArgumentException("Invalid property format: " + input);
            }

            return new KeyValuePair<string, string>(parts[0], string.Join("=", parts.Skip(1)));
        }

        public static JObject GetConfigFromEnv(Dictionary<string, string> mappings)
        {
            JObject json = null;
            var feedType = SourceUtility.GetFeedType(GetTokenValue(EnvVarFeedType, mappings, null));

            if (feedType != FileSystemStorageType.Unspecified)
            {
                json = new JObject();
                var sources = new JArray();
                var source = new JObject();
                sources.Add(source);
                json["sources"] = sources;

                json["username"] = GetTokenValue($"{EnvVarPrefix}USERNAME", mappings, string.Empty);
                json["useremail"] = GetTokenValue($"{EnvVarPrefix}USEREMAIL", mappings, string.Empty);

                // keep a default name to avoid confusion
                source["name"] = "envirnoment_feed";

                // load all env vars with SLEET_FEED_ into the source config
                // prefer mappings over env vars
                foreach (var pair in GetSleetEnvVars().Concat(mappings))
                {
                    if (pair.Key.StartsWith(EnvVarPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var innerKey = pair.Key.Substring(EnvVarPrefix.Length).ToLowerInvariant();

                        if (innerKey.Length > 0 && innerKey != "name")
                        {
                            source[innerKey] = ResolveTokens(pair.Value, mappings) ?? string.Empty;
                        }
                    }
                }
            }

            return json;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetSleetEnvVars()
        {
            foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
            {
                var key = pair.Key as string;

                if (!string.IsNullOrEmpty(key) && key.StartsWith(EnvVarPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var value = pair.Value as string;

                    if (!string.IsNullOrEmpty(value))
                    {
                        yield return new KeyValuePair<string, string>(key, value);
                    }
                }
            }
        }

        /// <summary>
        /// Replace string property values in a json file with $token$
        /// </summary>
        public static void ResolveTokensInSettingsJson(JObject json, Dictionary<string, string> mappings)
        {
            var properties = json.Descendants().Where(e => e.Type == JTokenType.Property)
                .Select(e => (JProperty)e)
                .ToList();

            foreach (var prop in properties)
            {
                if (prop.Value.Type == JTokenType.String)
                {
                    prop.Value = ResolveTokens(prop.Value.ToString(), mappings);
                }
            }
        }

        public static string GetTokenValue(string tokenName, Dictionary<string, string> mappings, string defaultValue)
        {
            if (TryResolveToken(tokenName, mappings, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public static bool TryResolveToken(string input, Dictionary<string, string> mappings, out string value)
        {
            value = null;

            if (!string.IsNullOrEmpty(input))
            {
                // Look up the value in the dictionary first
                if (mappings != null && mappings.TryGetValue(input, out var mappingValue))
                {
                    value = mappingValue;
                    return true;
                }

                // Try env vars
                value = Environment.GetEnvironmentVariable(input);

                if (!string.IsNullOrEmpty(value))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ResolveToken(string input, Dictionary<string, string> mappings, int depth)
        {
            // Look up the value
            if (TryResolveToken(input, mappings, out var resolvedValue))
            {
                // resolve further
                return ResolveTokens(resolvedValue, mappings, depth);
            }

            // Not found, return as it was
            return "$" + input + "$";
        }

        public static string ResolveTokens(string input, Dictionary<string, string> mappings, int depth = 0)
        {
            // noop if possible
            // avoid circular token resolution
            if (string.IsNullOrEmpty(input) || input.IndexOf('$') < 0 || depth > 100)
            {
                return input;
            }

            depth++;
            // Use nuget's tokenizer for .pp files
            var tokenizer = new Tokenizer(input);
            var result = new StringBuilder(input.Length);
            var token = tokenizer.Read();

            while (token != null)
            {
                if (token.Category == TokenCategory.Variable)
                {
                    result.Append(ResolveToken(token.Value, mappings, depth));
                }
                else
                {
                    result.Append(token.Value);
                }

                // get next
                token = tokenizer.Read();
            }

            return result.ToString();
        }

        /// <summary>
        /// Find sleet.json at the given path or search upwards from the current directory.
        /// </summary>
        public static string GetSleetJsonPathOrNull(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = FindFileInParents(Directory.GetCurrentDirectory(), "sleet.json");
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            var absolutePath = NuGetUriUtility.GetAbsolutePath(Directory.GetCurrentDirectory(), path);

            return absolutePath;
        }

        /// <summary>
        /// Search a dir and all parents for a file.
        /// </summary>
        private static string FindFileInParents(string root, string fileName)
        {
            var dir = new DirectoryInfo(root);

            while (dir != null)
            {
                var file = Path.Combine(dir.FullName, fileName);

                if (File.Exists(file))
                {
                    return file;
                }

                dir = dir.Parent;
            }

            return null;
        }
    }
}
