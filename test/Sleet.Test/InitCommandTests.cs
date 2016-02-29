using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Sleet.Test
{
    public class InitCommandTests
    {
        [Fact]
        public async Task InitCommand_Basic()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, new Uri(target.Root));
                var settings = new LocalSettings();

                var indexJsonOutput = new FileInfo(Path.Combine(target.Root, "index.json"));
                var settingsOutput = new FileInfo(Path.Combine(target.Root, "sleet.settings.json"));
                var autoCompleteOutput = new FileInfo(Path.Combine(target.Root, "autocomplete", "query"));
                var catalogOutput = new FileInfo(Path.Combine(target.Root, "catalog", "index.json"));
                var searchOutput = new FileInfo(Path.Combine(target.Root, "search", "query"));

                // Act
                var exitCode = await InitCommandTestHook.RunCore(settings, fileSystem, log);

                // Assert
                Assert.Equal(0, exitCode);
                Assert.True(indexJsonOutput.Exists);
                Assert.True(settingsOutput.Exists);
                Assert.True(autoCompleteOutput.Exists);
                Assert.True(catalogOutput.Exists);
                Assert.True(searchOutput.Exists);
            }
        }
    }
}
