using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Sleet.Tests
{
    /// <summary>
    /// Duplicated from SleetLib.Tests due to a VS 2017 RC2 Issue.
    /// </summary>
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
            var json = new JObject
            {
                { "username", "test" },
                { "useremail", "test@tempuri.org" }
            };
            var sourcesArray = new JArray();
            json.Add("sources", sourcesArray);

            var folderJson = new JObject
            {
                { "name", sourceName },
                { "type", "local" },
                { "path", sourcePath }
            };
            if (!string.IsNullOrEmpty(baseUri))
            {
                folderJson.Add("baseURI", baseUri);
            }

            sourcesArray.Add(folderJson);

            return json;
        }

        public static IEnumerable<FileInfo> GetJsonFiles(string root)
        {
            var dir = new DirectoryInfo(root);

            foreach (var file in dir.EnumerateFiles("*.json", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }
}