using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sleet
{
    public static class TemplateUtility
    {
        public static Task<string> LoadTemplate(string name, DateTimeOffset now, Uri baseUri)
        {
            return LoadTemplateFile($"template{name}.json", now, baseUri);
        }

        /// <summary>
        /// Load a template resource by file name. Use this for templates that are not json.
        /// </summary>
        public static async Task<string> LoadTemplateFile(string fileName, DateTimeOffset now, Uri baseUri)
        {
            using (var reader = new StreamReader(GetResource(fileName)))
            {
                var data = await reader.ReadToEndAsync().ConfigureAwait(false);

                return data
                    .Replace("$SLEETVERSION$", AssemblyVersionHelper.GetVersion().ToFullVersionString())
                    .Replace("$BASEURI$", UriUtility.RemoveTrailingSlash(baseUri).AbsoluteUri.TrimEnd('/'))
                    .Replace("$NOW$", now.GetDateString());
            }
        }

        public static Stream GetResource(string name)
        {
            var path = $"sleetlib.compiler.resources.{name}";

            foreach (var foundPath in typeof(TemplateUtility).GetTypeInfo().Assembly.GetManifestResourceNames())
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(path, foundPath))
                {
                    var stream = typeof(TemplateUtility).GetTypeInfo().Assembly.GetManifestResourceStream(foundPath);

                    return stream ?? throw new InvalidOperationException($"Unable to load embedded resource: {path}");
                }
            }

            throw new ArgumentException($"Unable to find embedded resource: {path}");
        }
    }
}
