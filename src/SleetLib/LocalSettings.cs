using System.IO;
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
                path = FindFileInParents(Directory.GetCurrentDirectory(), "sleet.json");
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"Unable to find source settings. File not found '{path}'.");
            }

            var json = JObject.Parse(File.ReadAllText(path));

            return new LocalSettings()
            {
                Json = json
            };
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