using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Sleet.Test
{
    public static class TestUtility
    {
        public static Stream GetResource(string name)
        {
            var path = $"Sleet.Test.compiler.resources.{name}";
            return typeof(TestUtility).GetTypeInfo().Assembly.GetManifestResourceStream(path);
        }

        public static JObject CreateConfigWithLocal(string sourceName, string sourcePath, string baseUri)
        {
            // Create the config template
            var json = new JObject();

            json.Add("username", "test");
            json.Add("useremail", "test@tempuri.org");

            var sourcesArray = new JArray();
            json.Add("sources", sourcesArray);

            var folderJson = new JObject();

            folderJson.Add("name", sourceName);
            folderJson.Add("type", "local");
            folderJson.Add("path", sourcePath);

            if (!string.IsNullOrEmpty(baseUri))
            {
                folderJson.Add("baseURI", baseUri);
            }

            sourcesArray.Add(folderJson);

            return json;
        }
    }
}