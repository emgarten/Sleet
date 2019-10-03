using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet.Tests;
using Xunit;

namespace Sleet.Integration.Test
{
    public class LocalFeedTests
    {
        [Fact]
        public async Task LocalFeed_RelativePath()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", "output", baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                await JsonUtility.SaveJsonAsync(new FileInfo(sleetConfigPath), sleetConfig);

                var settings = LocalSettings.Load(sleetConfigPath);
                var fileSystem = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "local") as PhysicalFileSystem;

                fileSystem.Should().NotBeNull();
                fileSystem.LocalRoot.Should().Be(Path.Combine(target.Root, "output") + Path.DirectorySeparatorChar);
            }
        }

        [Fact]
        public async Task LocalFeed_RelativePath_DefaultSleetJson()
        {
            var originalWorkingDir = Directory.GetCurrentDirectory();

            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", "output", baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.json");
                await JsonUtility.SaveJsonAsync(new FileInfo(sleetConfigPath), sleetConfig);


                try
                {
                    Directory.SetCurrentDirectory(target.Root);

                    //Load sleet.json file from working directory
                    var settings = LocalSettings.Load(path: null);
                    var fileSystem = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "local") as PhysicalFileSystem;

                    fileSystem.Should().NotBeNull();
                    fileSystem.LocalRoot.Should().Be(Path.Combine(target.Root, "output") + Path.DirectorySeparatorChar);

                }
                finally
                {
                    Directory.SetCurrentDirectory(originalWorkingDir);
                }
            }
        }
    }
}
