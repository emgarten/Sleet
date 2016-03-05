using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Sleet
{
    public static class TemplateUtility
    {
        public static string LoadTemplate(string name, DateTimeOffset now, Uri baseUri)
        {
            using (var reader = new StreamReader(GetResource($"template{name}.json")))
            {
                return reader.ReadToEnd()
                    .Replace("$BASEURI$", baseUri.AbsoluteUri.TrimEnd(new char[] { '/', '\\' }))
                    .Replace("$NOW$", now.GetDateString());
            }
        }

        public static Stream GetResource(string name)
        {
            var path = $"Sleet.compiler.resources.{name}";
            return typeof(Program).GetTypeInfo().Assembly.GetManifestResourceStream(path);
        }
    }
}
