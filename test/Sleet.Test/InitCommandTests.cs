using System;
using System.Collections.Generic;
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
            using (var cache = new LocalCache())
            {
                // Arrange
                var fileSystem = new MemoryFileSystem(cache, new Uri("https://tempuri.org/test/"));
                var settings = new LocalSettings();

                // Act
                var exitCode = await InitCommandTestHook.RunCore(settings, fileSystem);

                // Assert
                Assert.Equal(0, exitCode);
                Assert.True(fileSystem.Files.ContainsKey(new Uri("https://tempuri.org/test/index.json")));
            }
        }
    }
}
