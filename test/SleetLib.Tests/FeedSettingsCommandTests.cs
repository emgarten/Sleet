using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class FeedSettingsCommandTests
    {
        [Fact]
        public void GivenThatTheFeedIsNotInitializedVerifySettingsFails()
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
                    SourceSettings = new FeedSettings()
                };

                Func<Task> action = async () => await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { }, 
                    log: log,
                    token: context.Token);

                action.ShouldThrow<InvalidOperationException>("the feed is not initialized");
            }
        }

        [Fact]
        public void GivenBothGetAndSetArePassedVerifyFailure()
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
                    SourceSettings = new FeedSettings()
                };

                Func<Task> action = async () => await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: true,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                action.ShouldThrow<InvalidOperationException>("invalid combo");
            }
        }

        [Fact]
        public async Task GivenInvalidSettingFormatVerifyFailure()
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
                    SourceSettings = new FeedSettings()
                };

                var success = await InitCommand.RunAsync(settings, fileSystem, log);

                Func<Task> action = async () => await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { "blah" },
                    log: log,
                    token: context.Token);

                action.ShouldThrow<ArgumentException>("invalid format");
            }
        }

        [Fact]
        public async Task GivenAnEmptyFeedVerifyGetSettingsSucceeds()
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
                    SourceSettings = new FeedSettings()
                };

                var success = await InitCommand.RunAsync(settings, fileSystem, log);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task GivenSettingsAddedVerifyGetReturnsThem()
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

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                };

                var success = await InitCommand.RunAsync(settings, fileSystem, log);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { "catalogenabled:false", "a:1" },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem2,
                    unsetAll: false,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();
                log.GetMessages().Should().Contain("catalogenabled : false");
                log.GetMessages().Should().Contain("a : 1");
            }
        }

        [Fact]
        public async Task GivenDuplicateSetsVerifyFailure()
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

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                };

                var success = await InitCommand.RunAsync(settings, fileSystem, log);

                Func<Task> action = async () => await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: true,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { "a:1", "a:2" },
                    log: log,
                    token: context.Token);

                action.ShouldThrow<ArgumentException>("invalid combo");
            }
        }

        [Fact]
        public async Task GivenSettingsAddedVerifySingleGetReturnsJustOne()
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

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                };

                var success = await InitCommand.RunAsync(settings, fileSystem, log);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { "catalogenabled:false", "a:1" },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem2,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { "a" },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();
                log.GetMessages().Should().NotContain("catalogenabled : false");
                log.GetMessages().Should().Contain("a : 1");
            }
        }

        [Fact]
        public async Task GivenRequestForAMissingSettingVerifyNotFoundShown()
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

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                };

                var success = await InitCommand.RunAsync(settings, fileSystem, log);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { "b:2", "a:1" },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem2,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { "c" },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();
                log.GetMessages().Should().Contain("c : not found!");
            }
        }

        [Fact]
        public async Task GivenSettingsAddedVerifySingleUnsetClearsValue()
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

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                };

                var success = await InitCommand.RunAsync(settings, fileSystem, log);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { "b:2", "a:1" },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { "b" },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem2,
                    unsetAll: false,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();
                log.GetMessages().Should().NotContain("b : 2");
                log.GetMessages().Should().Contain("a : 1");
            }
        }

        [Fact]
        public async Task GivenSettingsAddedVerifySingleUnsetAllClearsValues()
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

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                };

                var success = await InitCommand.RunAsync(settings, fileSystem, log);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { "b:2", "a:1" },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: true,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem2,
                    unsetAll: false,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();
                log.GetMessages().Should().NotContain("b : 2");
                log.GetMessages().Should().NotContain("a : 1");
            }
        }

        [Fact]
        public async Task GivenSetAndUnsetCombinedVerifyResult()
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

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new FeedSettings()
                };

                var success = await InitCommand.RunAsync(settings, fileSystem, log);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { "b:2", "a:1" },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { "b" },
                    setSettings: new string[] { "c:3" },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    fileSystem2,
                    unsetAll: false,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();
                log.GetMessages().Should().NotContain("b : 2");
                log.GetMessages().Should().Contain("a : 1");
                log.GetMessages().Should().Contain("c : 3");
            }
        }
    }
}
