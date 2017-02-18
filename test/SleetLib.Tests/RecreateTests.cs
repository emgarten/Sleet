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
    public class RecreateTests
    {
        [Fact]
        public void GivenThatTheFeedIsNotInitializedVerifyRecreateFails()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var outputFolder = new TestFolder())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new SourceSettings()
                };

                Func<Task> action = async () => await RecreateCommand.RunAsync(settings, fileSystem, outputFolder, force: false, log: log);

                action.ShouldThrow<InvalidOperationException>("the feed is not initialized");
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasPackagesVerifyRecreateSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var cache3 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                var packageB = new TestNupkg("b", "2.0.0-beta+blah");
                packageA.Save(packagesFolder.Root);
                packageB.Save(packagesFolder.Root);

                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                // Recreate
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder, false, log);

                success.Should().BeTrue();

                var fileSystem3 = new PhysicalFileSystem(cache3, UriUtility.CreateUri(target.Root));

                var finalPackages = (await fileSystem3.GetFiles(log, CancellationToken.None))
                                    .Select(e => Path.GetFileName(e.EntityUri.LocalPath))
                                    .Where(e => e.EndsWith(".nupkg"))
                                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase);

                Directory.Exists(outputFolder).Should().BeFalse();

                finalPackages.ShouldBeEquivalentTo(new string[] { "a.1.0.0.nupkg", "b.2.0.0-beta.nupkg" });
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasMissingPackagesVerifyRecreateFails()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var cache3 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                var packageB = new TestNupkg("b", "2.0.0-beta+blah");
                packageA.Save(packagesFolder.Root);
                packageB.Save(packagesFolder.Root);

                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                var root = new DirectoryInfo(target);
                foreach (var file in root.GetFiles("a.1.0.0*", SearchOption.AllDirectories))
                {
                    // Corrupt the feed
                    file.Delete();
                }

                // Recreate
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder, false, log);

                success.Should().BeFalse();

                var fileSystem3 = new PhysicalFileSystem(cache3, UriUtility.CreateUri(target.Root));

                var finalPackages = (await fileSystem3.GetFiles(log, CancellationToken.None))
                                    .Select(e => Path.GetFileName(e.EntityUri.LocalPath))
                                    .Where(e => e.EndsWith(".nupkg"))
                                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase);

                Directory.Exists(outputFolder).Should().BeFalse();

                log.GetMessages().Should().Contain("Unable to recreate the feed due to errors download packages");
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasMissingPackagesVerifyRecreateSucceedsWithForce()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var cache3 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                var packageB = new TestNupkg("b", "2.0.0-beta+blah");
                packageA.Save(packagesFolder.Root);
                packageB.Save(packagesFolder.Root);

                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                var root = new DirectoryInfo(target);
                foreach (var file in root.GetFiles("a.1.0.0*", SearchOption.AllDirectories))
                {
                    // Corrupt the feed
                    file.Delete();
                }

                // Recreate
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder, force: true, log: log);

                success.Should().BeTrue();

                var fileSystem3 = new PhysicalFileSystem(cache3, UriUtility.CreateUri(target.Root));

                var finalPackages = (await fileSystem3.GetFiles(log, CancellationToken.None))
                                    .Select(e => Path.GetFileName(e.EntityUri.LocalPath))
                                    .Where(e => e.EndsWith(".nupkg"))
                                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase);

                Directory.Exists(outputFolder).Should().BeFalse();

                log.GetMessages().Should().Contain("Feed recreation complete.");
            }
        }
    }
}
