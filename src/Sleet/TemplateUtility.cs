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
                    .Replace("$SLEETVERSION$", Constants.SleetVersion.ToFullVersionString())
                    .Replace("$BASEURI$", baseUri.AbsoluteUri.TrimEnd(new char[] { '/', '\\' }))
                    .Replace("$NOW$", now.GetDateString());
            }
        }

        public static Stream GetResource(string name)
        {
            var path = $"sleet.compiler.resources.{name}";

            foreach (var foundPath in typeof(Program).GetTypeInfo().Assembly.GetManifestResourceNames())
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(path, foundPath))
                {
                    return typeof(Program).GetTypeInfo().Assembly.GetManifestResourceStream(foundPath);
                }
            }

            throw new ArgumentException($"Unable to find embedded resource: {path}");
        }
    }
}
