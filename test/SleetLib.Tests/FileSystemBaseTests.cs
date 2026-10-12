using System;
using System.IO;
using AwesomeAssertions;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class FileSystemBaseTests
    {
        [Fact]
        public void Get_WithBaseUriPath_MapsEntityUriToBaseUriAndRootPathToPhysicalRoot()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var root = UriUtility.CreateUri(Path.Combine(target.Root, "feed"), ensureTrailingSlash: true);
                var baseUri = new Uri("https://example/feed/");
                var fileSystem = new PhysicalFileSystem(cache, root, baseUri);

                var file = fileSystem.Get(new Uri("https://example/feed/path/index.json"));

                file.EntityUri.AbsoluteUri.Should().Be("https://example/feed/path/index.json");
                file.RootPath.AbsoluteUri.Should().Be(UriUtility.GetPath(root, "path/index.json").AbsoluteUri);
            }
        }

        [Fact]
        public void Get_WithRootPath_MapsEntityUriToBaseUriAndRootPathToPhysicalRoot()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var root = UriUtility.CreateUri(Path.Combine(target.Root, "feed"), ensureTrailingSlash: true);
                var baseUri = new Uri("https://example/feed/");
                var fileSystem = new PhysicalFileSystem(cache, root, baseUri);
                var rootPath = UriUtility.GetPath(root, "path/index.json");

                var file = fileSystem.Get(rootPath);

                file.RootPath.AbsoluteUri.Should().Be(rootPath.AbsoluteUri);
                file.EntityUri.AbsoluteUri.Should().Be("https://example/feed/path/index.json");
            }
        }

        [Fact]
        public void Get_WithUnrelatedUri_ThrowsInvalidOperationException()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var root = UriUtility.CreateUri(Path.Combine(target.Root, "feed"), ensureTrailingSlash: true);
                var fileSystem = new PhysicalFileSystem(cache, root, new Uri("https://example/feed/"));

                var ex = Assert.Throws<InvalidOperationException>(() => fileSystem.Get(new Uri("https://other/feed/index.json")));

                ex.Message.Should().Contain("URI does not match the feed root or baseURI");
            }
        }

        [Fact]
        public void GetRelativePath_AcceptsBaseUriAndRootUriButRejectsUnrelatedUri()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var root = UriUtility.CreateUri(Path.Combine(target.Root, "feed"), ensureTrailingSlash: true);
                var fileSystem = new PhysicalFileSystem(cache, root, new Uri("https://example/feed/"));

                fileSystem.GetRelativePath(new Uri("https://example/feed/a/index.json")).Should().Be("a/index.json");
                fileSystem.GetRelativePath(UriUtility.GetPath(root, "a/index.json")).Should().Be("a/index.json");
                Assert.Throws<InvalidOperationException>(() => fileSystem.GetRelativePath(new Uri("https://other/feed/a/index.json")));
            }
        }
    }
}
