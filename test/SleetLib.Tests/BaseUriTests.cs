using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Xunit;

namespace Sleet.Test
{
    public class BaseUriTests
    {
        [Fact]
        public async Task BaseUri_VerifyBaseUriIsSetForAllFilesAsync()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();

                var testPackage = new TestNupkg("packageA", "1.0.0");

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Save(packagesFolder.Root);

                var settings = LocalSettings.Load(sleetConfigPath);
                var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, "local");

                // Act
                var initSuccess = await InitCommand.RunAsync(settings, fileSystem, log);
                var pushSuccess = await PushCommand.RunAsync(settings, fileSystem, new List<string>() { zipFile.FullName }, false, log);

                var files = Directory.GetFiles(outputRoot, "*.json", SearchOption.AllDirectories);

                // Assert
                Assert.True(initSuccess, log.ToString());
                Assert.True(pushSuccess, log.ToString());

                foreach (var file in files)
                {
                    var fileJson = JsonUtility.LoadJson(new FileInfo(file));

                    foreach (var entityId in GetEntityIds(fileJson))
                    {
                        Assert.True(entityId.StartsWith(baseUri.AbsoluteUri), $"{entityId} in {file}");
                    }
                }
            }
        }

        /// <summary>
        /// Get all instance of @id outside of the context
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        private static IEnumerable<string> GetEntityIds(JObject json)
        {
            foreach (var node in json.Children())
            {
                if (node.Type == JTokenType.Property)
                {
                    var prop = (JProperty)node;

                    if (prop.Name != "@context")
                    {
                        if (prop.Value is JObject jObj)
                        {
                            foreach (var desc in jObj.DescendantsAndSelf())
                            {
                                var descProp = (JProperty)node;

                                if (descProp.Name == "@id")
                                {
                                    yield return descProp.Value.ToObject<string>();
                                }
                            }
                        }
                    }
                }
            }

            yield break;
        }
    }
}