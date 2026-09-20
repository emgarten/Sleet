using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class InitCommandTests
    {
        [Fact]
        public async Task GivenInitWithCatalogDisabledVerifyBasicOutputs()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var indexJsonOutput = new FileInfo(Path.Combine(target.Root, "index.json"));
                var settingsOutput = new FileInfo(Path.Combine(target.Root, "sleet.settings.json"));
                var autoCompleteOutput = new FileInfo(Path.Combine(target.Root, "autocomplete", "query"));
                var catalogOutput = new FileInfo(Path.Combine(target.Root, "catalog", "index.json"));
                var searchOutput = new FileInfo(Path.Combine(target.Root, "search", "query"));
                var packageIndexOutput = new FileInfo(Path.Combine(target.Root, "sleet.packageindex.json"));

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: log, token: CancellationToken.None);

                var rootFile = fileSystem.Get("index.json");
                var rootJson = await rootFile.GetJson(log, CancellationToken.None);

                success &= await FeedSettingsCommand.RunAsync(
                                    settings,
                                    fileSystem,
                                    unsetAll: false,
                                    getAll: true,
                                    getSettings: new string[] { },
                                    unsetSettings: new string[] { },
                                    setSettings: new string[] { },
                                    log: log,
                                    token: CancellationToken.None);

                // Assert
                success.Should().BeTrue();

                catalogOutput.Exists.Should().BeFalse();

                indexJsonOutput.Exists.Should().BeTrue();
                settingsOutput.Exists.Should().BeTrue();
                autoCompleteOutput.Exists.Should().BeTrue();
                searchOutput.Exists.Should().BeTrue();
                packageIndexOutput.Exists.Should().BeTrue();

                rootJson.ToString().Should().NotContain("catalog/index.json");
                rootJson.ToString().Should().NotContain("Catalog/3.0.0");

                log.GetMessages().Should().Contain("catalogpagesize : 1024");
                log.GetMessages().Should().Contain("catalogenabled : false");
                log.GetMessages().Should().Contain("symbolsfeedenabled : false");
            }
        }

        [Fact]
        public async Task GivenInitVerifyBasicOutputs()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var indexJsonOutput = new FileInfo(Path.Combine(target.Root, "index.json"));
                var settingsOutput = new FileInfo(Path.Combine(target.Root, "sleet.settings.json"));
                var autoCompleteOutput = new FileInfo(Path.Combine(target.Root, "autocomplete", "query"));
                var catalogOutput = new FileInfo(Path.Combine(target.Root, "catalog", "index.json"));
                var searchOutput = new FileInfo(Path.Combine(target.Root, "search", "query"));
                var packageIndexOutput = new FileInfo(Path.Combine(target.Root, "sleet.packageindex.json"));
                var symbolsIndexOutput = new FileInfo(Path.Combine(target.Root, "symbols", "packages", "index.json"));

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: true, log: log, token: CancellationToken.None);

                var rootFile = fileSystem.Get("index.json");
                var rootJson = await rootFile.GetJson(log, CancellationToken.None);

                success &= await FeedSettingsCommand.RunAsync(
                    settings,
                    fileSystem,
                    unsetAll: false,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                indexJsonOutput.Exists.Should().BeTrue();
                settingsOutput.Exists.Should().BeTrue();
                autoCompleteOutput.Exists.Should().BeTrue();
                catalogOutput.Exists.Should().BeTrue();
                searchOutput.Exists.Should().BeTrue();
                packageIndexOutput.Exists.Should().BeTrue();
                symbolsIndexOutput.Exists.Should().BeTrue();

                log.GetMessages().Should().Contain("catalogpagesize : 1024");
                log.GetMessages().Should().Contain("catalogenabled : true");
                log.GetMessages().Should().Contain("symbolsfeedenabled : true");

                rootJson.ToString().Should().Contain("catalog/index.json");
                rootJson.ToString().Should().Contain("Catalog/3.0.0");
                rootJson.ToString().Should().Contain("symbols/packages/index.json");
                rootJson.ToString().Should().Contain("ReadmeUriTemplate/6.13.0");
            }
        }

        [Fact]
        public async Task GivenInitWithSymbolsDisabledVerifyBasicOutputs()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var indexJsonOutput = new FileInfo(Path.Combine(target.Root, "index.json"));
                var settingsOutput = new FileInfo(Path.Combine(target.Root, "sleet.settings.json"));
                var autoCompleteOutput = new FileInfo(Path.Combine(target.Root, "autocomplete", "query"));
                var catalogOutput = new FileInfo(Path.Combine(target.Root, "catalog", "index.json"));
                var searchOutput = new FileInfo(Path.Combine(target.Root, "search", "query"));
                var packageIndexOutput = new FileInfo(Path.Combine(target.Root, "sleet.packageindex.json"));
                var symbolsIndexOutput = new FileInfo(Path.Combine(target.Root, "symbols", "packages", "index.json"));

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: false, log: log, token: CancellationToken.None);

                var rootFile = fileSystem.Get("index.json");
                var rootJson = await rootFile.GetJson(log, CancellationToken.None);

                success &= await FeedSettingsCommand.RunAsync(
                    settings,
                    fileSystem,
                    unsetAll: false,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                indexJsonOutput.Exists.Should().BeTrue();
                settingsOutput.Exists.Should().BeTrue();
                autoCompleteOutput.Exists.Should().BeTrue();
                catalogOutput.Exists.Should().BeTrue();
                searchOutput.Exists.Should().BeTrue();
                packageIndexOutput.Exists.Should().BeTrue();
                symbolsIndexOutput.Exists.Should().BeFalse();

                log.GetMessages().Should().Contain("catalogpagesize : 1024");
                log.GetMessages().Should().Contain("catalogenabled : true");
                log.GetMessages().Should().Contain("symbolsfeedenabled : false");

                rootJson.ToString().Should().Contain("catalog/index.json");
                rootJson.ToString().Should().Contain("Catalog/3.0.0");
                rootJson.ToString().Should().NotContain("symbols/packages/index.json");
            }
        }

        [Fact]
        public async Task GivenInitWithSymbolsAndCatalogDisabledVerifyBasicOutputs()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var indexJsonOutput = new FileInfo(Path.Combine(target.Root, "index.json"));
                var settingsOutput = new FileInfo(Path.Combine(target.Root, "sleet.settings.json"));
                var autoCompleteOutput = new FileInfo(Path.Combine(target.Root, "autocomplete", "query"));
                var catalogOutput = new FileInfo(Path.Combine(target.Root, "catalog", "index.json"));
                var searchOutput = new FileInfo(Path.Combine(target.Root, "search", "query"));
                var packageIndexOutput = new FileInfo(Path.Combine(target.Root, "sleet.packageindex.json"));
                var symbolsIndexOutput = new FileInfo(Path.Combine(target.Root, "symbols", "packages", "index.json"));

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: log, token: CancellationToken.None);

                var rootFile = fileSystem.Get("index.json");
                var rootJson = await rootFile.GetJson(log, CancellationToken.None);

                success &= await FeedSettingsCommand.RunAsync(
                    settings,
                    fileSystem,
                    unsetAll: false,
                    getAll: true,
                    getSettings: new string[] { },
                    unsetSettings: new string[] { },
                    setSettings: new string[] { },
                    log: log,
                    token: CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                indexJsonOutput.Exists.Should().BeTrue();
                settingsOutput.Exists.Should().BeTrue();
                autoCompleteOutput.Exists.Should().BeTrue();
                catalogOutput.Exists.Should().BeFalse();
                searchOutput.Exists.Should().BeTrue();
                packageIndexOutput.Exists.Should().BeTrue();
                symbolsIndexOutput.Exists.Should().BeFalse();

                log.GetMessages().Should().Contain("catalogpagesize : 1024");
                log.GetMessages().Should().Contain("catalogenabled : false");
                log.GetMessages().Should().Contain("symbolsfeedenabled : false");

                rootJson.ToString().Should().NotContain("catalog/index.json");
                rootJson.ToString().Should().NotContain("Catalog/3.0.0");
                rootJson.ToString().Should().NotContain("symbols/packages/index.json");
            }
        }

        [Fact]
        public async Task GivenInitVerifyFeedUIIsCreated()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var feedUIOutput = new FileInfo(Path.Combine(target.Root, "index.html"));

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: log, token: CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                feedUIOutput.Exists.Should().BeTrue();

                var html = File.ReadAllText(feedUIOutput.FullName);
                html.Should().Contain("search/query");
                html.Should().Contain("sleet.packageindex.json");
                html.Should().NotContain("$SLEETVERSION$");

                // Package metadata is untrusted. The UI must build the DOM with
                // textContent and route urls through safeUrl so that a published
                // package cannot inject script into the feed origin.
                html.Should().NotContain("innerHTML");
                html.Should().Contain("safeUrl(e.projectUrl)");
                html.Should().Contain("safeUrl(e.licenseUrl)");
                html.Should().Contain("safeUrl(e.iconUrl)");
            }
        }

        [Fact]
        public async Task GivenFeedUIDisabledVerifyIndexHtmlIsNotCreated()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var feedSettings = new FeedSettings() { FeedUIEnabled = false };

                var feedUIOutput = new FileInfo(Path.Combine(target.Root, "index.html"));
                var packageIndexOutput = new FileInfo(Path.Combine(target.Root, "sleet.packageindex.json"));

                // Act
                var success = await InitCommand.InitAsync(settings, fileSystem, feedSettings, log, CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                packageIndexOutput.Exists.Should().BeTrue();
                feedUIOutput.Exists.Should().BeFalse();
            }
        }

        [Fact]
        public async Task GivenFeedUIDisabledAfterInitVerifyIndexHtmlIsRemoved()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var settings = new LocalSettings();
                var feedUIOutput = new FileInfo(Path.Combine(target.Root, "index.html"));

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: log, token: CancellationToken.None);

                success.Should().BeTrue();
                feedUIOutput.Exists.Should().BeTrue();

                // Act
                using (var cache2 = new LocalCache())
                {
                    var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                    var feedSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem2, log, CancellationToken.None);
                    feedSettings.FeedUIEnabled = false;

                    success = await InitCommand.InitAsync(settings, fileSystem2, feedSettings, log, CancellationToken.None);
                }

                // Assert
                success.Should().BeTrue();
                feedUIOutput.Refresh();
                feedUIOutput.Exists.Should().BeFalse();
            }
        }

        [Fact]
        public async Task GivenACustomIndexHtmlVerifyInitDoesNotOverwriteIt()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var feedUIOutput = new FileInfo(Path.Combine(target.Root, "index.html"));
                var customHtml = "<html><body>custom</body></html>";
                File.WriteAllText(feedUIOutput.FullName, customHtml);

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: log, token: CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                feedUIOutput.Refresh();
                feedUIOutput.Exists.Should().BeTrue();
                File.ReadAllText(feedUIOutput.FullName).Should().Be(customHtml);
            }
        }
    }
}