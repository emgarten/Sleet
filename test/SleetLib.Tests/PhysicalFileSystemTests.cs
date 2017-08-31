using System;
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
        public async Task GivenAFileChangeVerifyCommitSetsHasChangesFalse()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");
                await a.Write(new JObject(), log, CancellationToken.None);
                a.HasChanges.Should().BeTrue();
                cache.Root.GetFiles().Length.Should().Be(1);

                await fileSystem.Commit(log, CancellationToken.None);
                a.HasChanges.Should().BeFalse();
                cache.Root.GetFiles().Length.Should().Be(1);
            }
        }

        [Fact]
        public void GivenAFileVerifyGetPathGivesTheSameFile()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");

                var test1 = fileSystem.Get("a.txt");
                var test2 = fileSystem.Get(new Uri(a.EntityUri.AbsoluteUri));
                var test3 = fileSystem.Get("/a.txt");

                ReferenceEquals(a, test1).Should().BeTrue();
                ReferenceEquals(a, test2).Should().BeTrue();
                ReferenceEquals(a, test3).Should().BeTrue();
            }
        }

        [Fact]
        public async Task GivenAFileVerifyExistsDoesNotDownload()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                File.WriteAllText(Path.Combine(target, "a.txt"), ".");

                var a = fileSystem.Get("a.txt");

                var exists = await a.Exists(log, CancellationToken.None);

                exists.Should().BeTrue();
                a.HasChanges.Should().BeFalse();
                cache.Root.GetFiles().Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GivenAFileCheckExistsThenDownloadVerifyHasChangesFalse()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                File.WriteAllText(Path.Combine(target, "a.txt"), ".");

                var a = fileSystem.Get("a.txt");

                var exists = await a.Exists(log, CancellationToken.None);
                await a.FetchAsync(log, CancellationToken.None);

                exists.Should().BeTrue();
                a.HasChanges.Should().BeFalse();
                cache.Root.GetFiles().Length.Should().Be(1);
            }
        }

        [Fact]
        public async Task GivenAFileExistsVerifyWriteOverWrites()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var path = Path.Combine(target, "a.txt");
                File.WriteAllText(path, ".");

                var a = fileSystem.Get("a.txt");
                await a.Write(new JObject(), log, CancellationToken.None);
                a.HasChanges.Should().BeTrue();
                cache.Root.GetFiles().Length.Should().Be(1);

                await fileSystem.Commit(log, CancellationToken.None);
                File.ReadAllText(path).Should().StartWith("{");
            }
        }

        [Fact]
        public async Task GivenAFileWriteVerifyExistsAfter()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");
                await a.Write(new JObject(), log, CancellationToken.None);
                var exists = await a.Exists(log, CancellationToken.None);

                a.HasChanges.Should().BeTrue();
                cache.Root.GetFiles().Length.Should().Be(1);
                exists.Should().BeTrue();
            }
        }

        [Fact]
        public async Task GivenAFileWriteVerifyGetAfter()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");
                await a.Write(new JObject(), log, CancellationToken.None);
                var json = await a.GetJson(log, CancellationToken.None);

                a.HasChanges.Should().BeTrue();
                cache.Root.GetFiles().Length.Should().Be(1);
                json.ToString().Should().Be((new JObject()).ToString());
            }
        }

        [Fact]
        public async Task GivenAFileWriteThenDeleteVerifyDoesNotExist()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");
                await a.Write(new JObject(), log, CancellationToken.None);
                a.Delete(log, CancellationToken.None);

                a.HasChanges.Should().BeTrue();
                cache.Root.GetFiles().Should().BeEmpty();

                await fileSystem.Commit(log, CancellationToken.None);
                var path = Path.Combine(target, "a.txt");
                File.Exists(path).Should().BeFalse();
            }
        }

        [Fact]
        public async Task GivenAFileWriteThenDeleteMultipleTimesVerifyDoesNotExist()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var a = fileSystem.Get("a.txt");

                await a.Write(new JObject(), log, CancellationToken.None);
                a.Delete(log, CancellationToken.None);

                await a.Write(new JObject(), log, CancellationToken.None);
                a.Delete(log, CancellationToken.None);

                await a.Write(new JObject(), log, CancellationToken.None);
                await a.Write(new JObject(), log, CancellationToken.None);
                await a.Write(new JObject(), log, CancellationToken.None);
                a.Delete(log, CancellationToken.None);

                a.HasChanges.Should().BeTrue();
                cache.Root.GetFiles().Should().BeEmpty();

                await fileSystem.Commit(log, CancellationToken.None);
                var path = Path.Combine(target, "a.txt");
                File.Exists(path).Should().BeFalse();
            }
        }

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
