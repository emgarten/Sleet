using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class FileSystemTests
    {
        [Fact]
        public async Task FileSystem_VerifyFileSystemResetOnLock()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem1 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var lockMessage = Guid.NewGuid().ToString();

                await InitCommand.RunAsync(settings, fileSystem1, log);

                // Verify that files work normally
                var testFile = fileSystem1.Get("test.json");
                await testFile.GetJsonOrNull(log, CancellationToken.None);

                var testFile2 = fileSystem1.Get("test2.json");
                fileSystem1.Files.Count.Should().BeGreaterThan(1);

                // Lock the feed to reset it
                var lockObj1 = await SourceUtility.VerifyInitAndLock(settings, fileSystem1, lockMessage, log, CancellationToken.None);
                lockObj1.IsLocked.Should().BeTrue();

                // 1 file should be found since it loads the index
                fileSystem1.Files.Count.Should().Be(1);
                InvalidOperationException failureEx = null;

                try
                {
                    // Verify the old file no longer works
                    await testFile.GetJsonOrNull(log, CancellationToken.None);
                    await testFile2.GetJsonOrNull(log, CancellationToken.None);
                }
                catch (InvalidOperationException ex)
                {
                    failureEx = ex;
                }

                failureEx.Should().NotBeNull();
            }
        }
    }
}