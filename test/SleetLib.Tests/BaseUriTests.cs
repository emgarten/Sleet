using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Sleet.Test.Common;
using Xunit;

namespace SleetLib.Tests
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
                await JsonUtility.SaveJsonAsync(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Save(packagesFolder.Root);

                var settings = LocalSettings.Load(sleetConfigPath);
                var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, "local");

                // Act
                var initSuccess = await InitCommand.RunAsync(settings, fileSystem, log);
                var pushSuccess = await PushCommand.RunAsync(settings, fileSystem, new List<string>() { zipFile.FullName }, false, false, log);


                // Assert
                Assert.True(initSuccess, log.ToString());
                Assert.True(pushSuccess, log.ToString());

                var files = Directory.GetFiles(outputRoot, "*.json", SearchOption.AllDirectories);
                await BaseURITestUtil.VerifyBaseUris(files, baseUri);
            }
        }
    }
}