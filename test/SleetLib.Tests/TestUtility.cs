using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Sleet;
using Sleet.Test.Common;
using Xunit;

namespace SleetLib.Tests
{
    public static class TestUtility
    {
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
            return SleetTestContext.GetPackageInput(zipFile);
        }

        /// <summary>
        /// Add the service index resources and setting that the removed symbols server added to a local feed.
        /// </summary>
        public static void AddLegacySymbolsServer(string feedRoot)
        {
            var baseUri = UriUtility.CreateUri(feedRoot);

            UpdateJson(Path.Combine(feedRoot, "index.json"), json =>
            {
                var resources = (JArray)json["resources"];
                resources.Add(new JObject
                {
                    ["@id"] = UriUtility.GetPath(baseUri, "symbols/packages/index.json").AbsoluteUri,
                    ["@type"] = "http://schema.emgarten.com/sleet#SymbolsPackageIndex/1.0.0",
                    ["comment"] = "Packages indexed in the symbols feed."
                });
                resources.Add(new JObject
                {
                    ["@id"] = UriUtility.GetPath(baseUri, "symbols/").AbsoluteUri,
                    ["@type"] = "http://schema.emgarten.com/sleet#SymbolsServer/1.0.0",
                    ["comment"] = "Symbols server containing dll and pdb files."
                });
            });

            UpdateJson(Path.Combine(feedRoot, "sleet.settings.json"), json =>
            {
                var values = FeedSettingsUtility.GetSettings(json);
                values["symbolsfeedenabled"] = "true";
                FeedSettingsUtility.Set(json, values);
            });
        }

        /// <summary>
        /// Add a symbols package to a local feed the way the removed symbols server stored it.
        /// </summary>
        public static void AddLegacySymbolsPackage(string feedRoot, string packagesRoot, string id, string version)
        {
            var symbolsPackage = new TestNupkg(id, version);
            symbolsPackage.Nuspec.IsSymbolPackage = true;
            var nupkg = symbolsPackage.Save(packagesRoot);

            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var nupkgPath = Path.Combine(feedRoot, "symbols", "packages", id.ToLowerInvariant(), normalizedVersion.ToLowerInvariant(), $"{id}.{normalizedVersion}.symbols.nupkg".ToLowerInvariant());
            Directory.CreateDirectory(Path.GetDirectoryName(nupkgPath));
            nupkg.CopyTo(nupkgPath, overwrite: true);

            UpdateJson(Path.Combine(feedRoot, "sleet.packageindex.json"), json =>
            {
                if (json["symbols"] is not JObject symbols)
                {
                    symbols = new JObject();
                    json["symbols"] = symbols;
                }

                if (symbols[id] is not JArray versions)
                {
                    versions = new JArray();
                    symbols[id] = versions;
                }

                versions.Add(normalizedVersion);
            });
        }

        private static void UpdateJson(string path, Action<JObject> update)
        {
            var json = JObject.Parse(File.ReadAllText(path));
            update(json);
            File.WriteAllText(path, json.ToString());
        }
    }
}