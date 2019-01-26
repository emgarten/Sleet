using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public static class TestUtility
    {
        public static Stream GetResource(string name)
        {
            var path = $"SleetLib.Tests.compiler.resources.{name}";
            return typeof(TestUtility).GetTypeInfo().Assembly.GetManifestResourceStream(path);
        }

        public static byte[] GetBytes(this Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
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

        public static async Task WalkJsonAsync(string root, Action<FileInfo, JObject, string> walker)
        {
            var valid = false;

            foreach (var file in GetJsonFiles(root))
            {
                var json = await JsonUtility.LoadJsonAsync(file);
                var tokens = json.Descendants().ToArray();

                foreach (var token in tokens)
                {
                    if (token.Type == JTokenType.String
                        || token.Type == JTokenType.Uri)
                    {
                        valid = true;
                        walker(file, json, token.Value<string>());
                    }
                }
            }

            // Ensure that the input was valid
            Assert.True(valid);
        }

        public static PackageInput GetPackageInput(string id, SleetTestContext testContext)
        {
            return GetPackageInput(id, testContext, isSymbols: false);
        }

        public static PackageInput GetPackageInput(string id, SleetTestContext testContext, bool isSymbols)
        {
            var testPackage = new TestNupkg(id, "1.0.0");
            testPackage.Nuspec.IsSymbolPackage = isSymbols;
            var zipFile = testPackage.Save(testContext.Packages);
            return testContext.GetPackageInput(zipFile);
        }
    }
}