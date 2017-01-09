using System;
using System.IO;
using System.Reflection;

namespace Sleet
{
    public static class TemplateUtility
    {
        public static string LoadTemplate(string name, DateTimeOffset now, Uri baseUri)
        {
            using (var reader = new StreamReader(GetResource($"template{name}.json")))
            {
                return reader.ReadToEnd()
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

                    return stream;
                }
            }

            throw new ArgumentException($"Unable to find embedded resource: {path}");
        }
    }
}