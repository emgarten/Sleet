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
        public async Task GivenInitVerifyGalleryIsCreated()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                var galleryOutput = new FileInfo(Path.Combine(target.Root, "index.html"));

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: log, token: CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                galleryOutput.Exists.Should().BeTrue();

                var html = File.ReadAllText(galleryOutput.FullName);
                html.Should().Contain("sleet.packageindex.json");
                html.Should().Contain("registration/");
                html.Should().NotContain("$SLEETVERSION$");

                // Packages, versions and a single version are separate views so
                // that each one can be linked to directly.
                html.Should().Contain("#package/");
                html.Should().Contain(".nuspec");

                // The gallery reads the package index and the registrations, it
                // must not depend on search which can be pointed off feed.
                html.Should().NotContain("search/query");

                // Package metadata is untrusted. The UI must build the DOM with
                // textContent and route urls through safeUrl so that a published
                // package cannot inject script into the feed origin.
                html.Should().NotContain("innerHTML");
                html.Should().Contain("safeUrl(e.projectUrl)");
                html.Should().Contain("safeUrl(e.licenseUrl)");
            }
        }

        [Fact]
        public async Task GivenGalleryDisabledVerifyIndexHtmlIsNotCreated()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var feedSettings = new FeedSettings() { GalleryEnabled = false };

                var galleryOutput = new FileInfo(Path.Combine(target.Root, "index.html"));
                var packageIndexOutput = new FileInfo(Path.Combine(target.Root, "sleet.packageindex.json"));

                // Act
                var success = await InitCommand.InitAsync(settings, fileSystem, feedSettings, log, CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                packageIndexOutput.Exists.Should().BeTrue();
                galleryOutput.Exists.Should().BeFalse();
            }
        }

        [Fact]
        public async Task GivenGalleryDisabledAfterInitVerifyIndexHtmlIsRemoved()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var settings = new LocalSettings();
                var galleryOutput = new FileInfo(Path.Combine(target.Root, "index.html"));

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: log, token: CancellationToken.None);

                success.Should().BeTrue();
                galleryOutput.Exists.Should().BeTrue();

                // Act
                using (var cache2 = new LocalCache())
                {
                    var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                    var feedSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem2, log, CancellationToken.None);
                    feedSettings.GalleryEnabled = false;

                    success = await InitCommand.InitAsync(settings, fileSystem2, feedSettings, log, CancellationToken.None);
                }

                // Assert
                success.Should().BeTrue();
                galleryOutput.Refresh();
                galleryOutput.Exists.Should().BeFalse();
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

                var galleryOutput = new FileInfo(Path.Combine(target.Root, "index.html"));
                var customHtml = "<html><body>custom</body></html>";
                File.WriteAllText(galleryOutput.FullName, customHtml);

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: log, token: CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                galleryOutput.Refresh();
                galleryOutput.Exists.Should().BeTrue();
                File.ReadAllText(galleryOutput.FullName).Should().Be(customHtml);
            }
        }

        [Fact]
        public async Task GivenPushOnAnInitializedFeedVerifyGalleryIsNotTouched()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var settings = new LocalSettings();
                var galleryOutput = new FileInfo(Path.Combine(target.Root, "index.html"));

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: log, token: CancellationToken.None);

                success.Should().BeTrue();
                galleryOutput.Exists.Should().BeTrue();

                // Replace the page so that any rewrite during push is detectable.
                var customHtml = "<html><body>custom</body></html>";
                File.WriteAllText(galleryOutput.FullName, customHtml);

                var testPackage = new TestNupkg("packageA", "1.0.0");
                var zipFile = testPackage.Save(packagesFolder.Root);

                // Act
                using (var cache2 = new LocalCache())
                {
                    var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                    success = await PushCommand.RunAsync(settings, fileSystem2, new List<string>() { zipFile.FullName }, force: false, skipExisting: false, log: log);

                    // Assert
                    success.Should().BeTrue();

                    // The gallery is only relevant to init and recreate. Push must not
                    // read or write it, so it should never be requested from the feed.
                    // index.json is requested on every push and acts as a control to
                    // show that requested files are tracked here.
                    fileSystem2.Files.Keys.Should().Contain(fileSystem2.GetPath("index.json"));
                    fileSystem2.Files.Keys.Should().NotContain(fileSystem2.GetPath("index.html"));
                }

                File.ReadAllText(galleryOutput.FullName).Should().Be(customHtml);
            }
        }
    }
}