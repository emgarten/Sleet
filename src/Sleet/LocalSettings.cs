using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    public class LocalSettings
    {
        public JObject Json { get; set; }

        public static LocalSettings Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "sleet.json");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Unable to find source settings. File not found '{path}'.");
            }

            var json = JObject.Parse(File.ReadAllText(path));

            return new LocalSettings()
            {
                Json = json
            };
        }
    }
}
