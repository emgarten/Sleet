using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class DownloadTests
    {
        [Fact]
        public void GivenThatTheFeedIsNotInitializedVerifyCommandFails()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var outputFolder = new TestFolder())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                Func<Task> action = async () => await DownloadCommand.RunAsync(settings, fileSystem, outputFolder, false, log);

                action.ShouldThrow<InvalidOperationException>("the feed is not initialized");
            }
        }

        [Fact]
        public async Task GivenThatTheFeedIsEmptyVerifyDownloadCommandSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var outputFolder = new TestFolder())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, log);

                var success = await DownloadCommand.RunAsync(settings, fileSystem2, outputFolder, false, log);

                success.ShouldBeEquivalentTo(true, "the feed is valid");

                Directory.GetFiles(outputFolder, "*.nupkg", SearchOption.AllDirectories).Length.ShouldBeEquivalentTo(0, "the feed is empty");

                log.GetMessages().Should().Contain("The feed does not contain any packages");
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasPackagesVerifyDownloadCommandSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var outputFolder = new TestFolder())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                var packageB = new TestNupkg("b", "2.0.0-beta+blah");
                packageA.Save(packagesFolder.Root);
                packageB.Save(packagesFolder.Root);

                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                var success = await DownloadCommand.RunAsync(settings, fileSystem2, outputFolder, false, log);

                var fileNames = Directory.GetFiles(outputFolder, "*.nupkg", SearchOption.AllDirectories)
                    .Select(e => Path.GetFileName(e))
                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                success.ShouldBeEquivalentTo(true, "the feed is valid");

                fileNames.ShouldBeEquivalentTo(new[] { "a.1.0.0.nupkg", "b.2.0.0-beta.nupkg" });

                log.GetMessages().Should().NotContain("The feed does not contain any packages");
                log.GetMessages().Should().Contain("a.1.0.0.nupkg");
                log.GetMessages().Should().Contain("b.2.0.0-beta.nupkg");
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasMissingPackagesVerifyExistingPackagesAreDownloaded()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var outputFolder = new TestFolder())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, log);

                var expected = new List<string>();

                for (var i = 0; i < 100; i++)
                {
                    var package = new TestNupkg("a", $"{i}.0.0");
                    package.Save(packagesFolder);

                    if (i != 50)
                    {
                        expected.Add($"a.{i}.0.0.nupkg");
                    }
                }

                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                var root = new DirectoryInfo(target);
                foreach (var file in root.GetFiles("a.50.0.0*", SearchOption.AllDirectories))
                {
                    // Corrupt the feed
                    file.Delete();
                }

                var success = await DownloadCommand.RunAsync(settings, fileSystem2, outputFolder, false, log);

                var fileNames = Directory.GetFiles(outputFolder, "*.nupkg", SearchOption.AllDirectories)
                    .Select(e => Path.GetFileName(e))
                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                success.ShouldBeEquivalentTo(false, "the feed is not valid");

                fileNames.ShouldBeEquivalentTo(expected, "all files but the deleted one");

                log.GetMessages().Should().NotContain("The feed does not contain any packages");
                log.GetMessages().Should().Contain("Failed to download all packages!");

                foreach (var file in expected)
                {
                    log.GetMessages().Should().Contain(file);
                }
            }
        }
    }
}
