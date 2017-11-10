using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    public class LocalSettings
    {
        /// <summary>
        /// Entire sleet.json file.
        /// </summary>
        public JObject Json { get; set; }

        /// <summary>
        /// Feed lock wait time.
        /// config/feedLockTimeoutMinutes
        /// </summary>
        public TimeSpan FeedLockTimeout { get; set; }

        public static LocalSettings Load(JObject json)
        {
            return new LocalSettings()
            {
                Json = json,
                FeedLockTimeout = GetFeedLockTimeout(json)
            };
        }

        public static LocalSettings Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = FindFileInParents(Directory.GetCurrentDirectory(), "sleet.json");
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"Unable to find source settings. File not found '{path}'.");
            }

            var json = JObject.Parse(File.ReadAllText(path));

            return Load(json);
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