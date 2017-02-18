using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class DestroyTests
    {
        [Fact]
        public void GivenThatIWantToDestroyAFeedVerifyAnEmptyFeedThrows()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var outputFolder = new TestFolder())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                Func<Task> task = async () => await DestroyCommand.RunAsync(settings, fileSystem, log);

                task.ShouldThrow<InvalidOperationException>();
            }
        }

        [Fact]
        public async Task GivenThatIWantToDestroyAFeedVerifyAFeedWithNupkgsSucceeds()
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
                var root = new DirectoryInfo(target);

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                var packageB = new TestNupkg("b", "2.0.0-beta+blah");
                packageA.Save(packagesFolder.Root);
                packageB.Save(packagesFolder.Root);

                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                var success = await DestroyCommand.RunAsync(settings, fileSystem2, log);

                var files = root.GetFiles("*", SearchOption.AllDirectories);
                var dirs = root.GetDirectories();

                success.ShouldBeEquivalentTo(true, "the command should exit without errors");

                files.Length.ShouldBeEquivalentTo(0, "all files should be gone");
                dirs.Length.ShouldBeEquivalentTo(0, "all directories should be gone");
            }
        }
    }
}
