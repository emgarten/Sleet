using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class PhysicalFileSystemTests
    {
        [Fact]
        public async Task GivenThatICreateAndDeleteAFileInTheSameSessionVerifyItIsRemoved()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");

                await a.Write(new JObject(), log, CancellationToken.None);

                a.Delete(log, CancellationToken.None);

                await fileSystem.Commit(log, CancellationToken.None);

                File.Exists(a.RootPath.LocalPath).Should().BeFalse("the file was deleted");
            }
        }

        [Fact]
        public async Task GivenThatIDeleteAFileVerifyItIsRemoved()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");
                await a.Write(new JObject(), log, CancellationToken.None);
                await fileSystem.Commit(log, CancellationToken.None);

                File.Exists(a.RootPath.LocalPath).Should().BeTrue("the file was not deleted yet");

                a = fileSystem2.Get("a.txt");
                a.Delete(log, CancellationToken.None);
                await fileSystem2.Commit(log, CancellationToken.None);

                File.Exists(a.RootPath.LocalPath).Should().BeFalse("the file was deleted");
            }
        }

        [Fact]
        public async Task GivenThatIDestroyAFeedItIsNowEmpty()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");
                await a.Write(new JObject(), log, CancellationToken.None);
                await fileSystem.Commit(log, CancellationToken.None);

                File.Exists(a.RootPath.LocalPath).Should().BeTrue("the file was not deleted yet");

                await fileSystem2.Destroy(log, CancellationToken.None);
                await fileSystem2.Commit(log, CancellationToken.None);

                Directory.Exists(target).Should().BeFalse("all files were deleted");
            }
        }

        [Fact]
        public async Task GivenThatICallGetFilesReturnAKnownFile()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");
                await a.Write(new JObject(), log, CancellationToken.None);
                await fileSystem.Commit(log, CancellationToken.None);

                File.Exists(a.RootPath.LocalPath).Should().BeTrue("the file was not deleted yet");

                var results = await fileSystem2.GetFiles(log, CancellationToken.None);

                results.Select(e => Path.GetFileName(e.EntityUri.LocalPath)).ShouldBeEquivalentTo(new[] { "a.txt" });
            }
        }
    }
}
