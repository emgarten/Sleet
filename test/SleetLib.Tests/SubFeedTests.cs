using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class SubFeedTests
    {
        [Fact]
        public void SubFeed_VerifySubFeedPathMustBeOnPath()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var root = UriUtility.CreateUri(target.Root);
                var fileSystem = new PhysicalFileSystem(cache, root, root, feedSubPath: "feedA");

                Assert.Throws<ArgumentException>(() => SourceUtility.ValidateFileSystem(fileSystem));
            }
        }

        [Fact]
        public void SubFeed_VerifySubFeedPath()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var root = UriUtility.CreateUri(target.Root, "feedA");
                var fileSystem = new PhysicalFileSystem(cache, root, root, feedSubPath: "feedA");

                fileSystem.Root.Should().Be(UriUtility.EnsureTrailingSlash(root));
                fileSystem.LocalRoot.Should().StartWith(Path.Combine(target.Root, "feedA"));

                fileSystem.Get("index.json").EntityUri.Should().Be(UriUtility.GetPath(root, "index.json"));
            }
        }

        [Fact]
        public async Task SubFeed_InitTwoFeedsVerifyNoFilesInRoot()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();
                var feedSettings = new FeedSettings();
                var rootFeedA = UriUtility.CreateUri(target.Root, "feedA");
                var rootFeedB = UriUtility.CreateUri(target.Root, "feedB");
                var fileSystem = new PhysicalFileSystem(cache, rootFeedA, rootFeedA, feedSubPath: "feedA");
                var fileSystem2 = new PhysicalFileSystem(cache2, rootFeedB, rootFeedB, feedSubPath: "feedB");

                // Init feeds
                var success = await InitCommand.InitAsync(settings, fileSystem, feedSettings, log, CancellationToken.None);
                success &= await InitCommand.InitAsync(settings, fileSystem2, feedSettings, log, CancellationToken.None);

                success.Should().BeTrue();
                target.RootDirectory.GetFiles().Should().BeEmpty();
                target.RootDirectory.GetDirectories().Select(e => e.Name).ShouldBeEquivalentTo(new[] { "feedA", "feedB" });
            }
        }

        [Fact]
        public async Task SubFeed_InitTwoFeedsDestroyOneVerifyFirst()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();
                var feedSettings = new FeedSettings();
                var rootFeedA = UriUtility.CreateUri(target.Root, "feedA");
                var rootFeedB = UriUtility.CreateUri(target.Root, "feedB");
                var fileSystem = new PhysicalFileSystem(cache, rootFeedA, rootFeedA, feedSubPath: "feedA");
                var fileSystem2 = new PhysicalFileSystem(cache2, rootFeedB, rootFeedB, feedSubPath: "feedB");

                // Init feeds
                var success = await InitCommand.InitAsync(settings, fileSystem, feedSettings, log, CancellationToken.None);
                success &= await InitCommand.InitAsync(settings, fileSystem2, feedSettings, log, CancellationToken.None);

                // Destroy feed 2
                success &= await DestroyCommand.Destroy(settings, fileSystem2, log, CancellationToken.None);

                // Validate feed 1
                success &= await ValidateCommand.Validate(settings, fileSystem, log, CancellationToken.None);

                success.Should().BeTrue();
                target.RootDirectory.GetFiles().Should().BeEmpty();
                target.RootDirectory.GetDirectories().Select(e => e.Name).ShouldBeEquivalentTo(new[] { "feedA" });
            }
        }
    }
}
