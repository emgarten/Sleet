using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
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
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, log: log, token: CancellationToken.None);

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
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, log: log, token: CancellationToken.None);

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
                log.GetMessages().Should().NotContain("symbolsfeedenabled");

                rootJson.ToString().Should().Contain("catalog/index.json");
                rootJson.ToString().Should().Contain("Catalog/3.0.0");
                rootJson.ToString().Should().NotContain("symbols/packages/index.json");
                rootJson.ToString().Should().NotContain("SymbolsServer");
                rootJson.ToString().Should().Contain("ReadmeUriTemplate/6.13.0");
            }
        }

        [Fact]
        public async Task GivenInitOnInitializedFeedVerifyCommandSucceedsWithoutDuplicatingResources()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, log);
                var rootFile = fileSystem.Get("index.json");
                var before = await rootFile.GetJson(log, TestContext.Current.CancellationToken);
                var beforeCount = before["resources"].Count();

                // InitCommand's "already initialized" check compares a DeepClone with JToken.Equals, which is
                // reference equality for JObject, so re-running init rewrites the feed instead of throwing.
                var success = await InitCommand.RunAsync(settings, fileSystem, log);
                var after = await rootFile.GetJson(log, TestContext.Current.CancellationToken);

                success.Should().BeTrue();
                after["resources"].Count().Should().Be(beforeCount);
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
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, log: log, token: CancellationToken.None);

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
            }
        }

        [Fact]
        public async Task GivenInitVerifyGalleryPolicyAllowsOnlyItsOwnScriptsAndStyles()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, log: log, token: TestContext.Current.CancellationToken);
                var html = File.ReadAllText(Path.Combine(target.Root, "index.html"));

                // Assert
                success.Should().BeTrue();

                // Browsers hash the text between the tags after the parser normalizes line endings to LF.
                var text = html.Replace("\r\n", "\n").Replace('\r', '\n');
                string Hashes(string tag) => string.Join(" ", Regex.Matches(text, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline)
                    .Select(m => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(m.Groups[1].Value)))}'"));

                var expected = $"default-src 'none'; script-src {Hashes("script")}; style-src {Hashes("style")}; "
                    + "img-src 'self' data: https: http:; connect-src 'self'; base-uri 'none'; form-action 'none'";

                // Editing a script or style block changes its hash, copy the expected policy into templateGallery.html.
                var policy = Regex.Match(html, "<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]*)\">").Groups[1].Value;
                policy.Should().Be(expected, "the gallery must only run its own script and style blocks");
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

                var rootJson = await fileSystem.Get("index.json").GetJson(log, CancellationToken.None);

                // Assert
                success.Should().BeTrue();
                packageIndexOutput.Exists.Should().BeTrue();
                galleryOutput.Exists.Should().BeFalse();
                rootJson.ToString().Should().NotContain("PackageDetailsUriTemplate");
            }
        }

        [Fact]
        public async Task GivenInitWithSettingsVerifyTheyAreAppliedToTheNewFeed()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var setSettings = new[] { "galleryenabled:false", "badgesenabled:true", "catalogenabled:true", "catalogpagesize:10" };

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, setSettings, log, TestContext.Current.CancellationToken);

                var settingsJson = JObject.Parse(File.ReadAllText(Path.Combine(target.Root, "sleet.settings.json")));
                var rootJson = JObject.Parse(File.ReadAllText(Path.Combine(target.Root, "index.json")));

                // Assert
                success.Should().BeTrue();

                FeedSettingsUtility.GetSettings(settingsJson).Should().BeEquivalentTo(new Dictionary<string, string>()
                {
                    { "badgesenabled", "true" },
                    { "catalogenabled", "true" },
                    { "catalogpagesize", "10" },
                    { "galleryenabled", "false" }
                });

                File.Exists(Path.Combine(target.Root, "index.html")).Should().BeFalse();
                File.Exists(Path.Combine(target.Root, "catalog", "index.json")).Should().BeTrue();
                rootJson.ToString().Should().NotContain("PackageDetailsUriTemplate");
            }
        }

        [Theory]
        [InlineData(false, "galeryenabled:false", "Unknown feed setting 'galeryenabled'. Use one of: *")]
        [InlineData(false, "galleryenabled:no", "Invalid value 'no' for 'galleryenabled'. Use true or false.")]
        [InlineData(false, "catalogpagesize:big", "Invalid value 'big' for 'catalogpagesize'. Use a whole number.")]
        [InlineData(false, "galleryenabled", "Value must be in the form {key}:{value}. Invalid: 'galleryenabled'")]
        [InlineData(true, "catalogenabled:false", "Invalid combination of arguments. --with-catalog may not be combined with catalogenabled:false.")]
        public async Task GivenInitWithAnInvalidSettingVerifyTheFeedIsNotCreated(bool enableCatalog, string setting, string expectedMessage)
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                // Act
                Func<Task> action = async () => await InitCommand.RunAsync(settings, fileSystem, enableCatalog, new[] { setting }, log, TestContext.Current.CancellationToken);

                // Assert
                await action.Should().ThrowAsync<ArgumentException>().WithMessage(expectedMessage);

                File.Exists(Path.Combine(target.Root, "index.json")).Should().BeFalse();
                File.Exists(Path.Combine(target.Root, "sleet.settings.json")).Should().BeFalse();
            }
        }

        [Fact]
        public async Task GivenInitVerifyPackageDetailsUriTemplatePointsToGallery()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root), UriUtility.CreateUri("https://example.com/feed/"));
                var settings = new LocalSettings();

                // Act
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, log: log, token: CancellationToken.None);

                var rootJson = await fileSystem.Get("index.json").GetJson(log, CancellationToken.None);
                var resources = ((JArray)rootJson["resources"]).Cast<JObject>();

                // Assert
                success.Should().BeTrue();

                // NuGet fills in the template by replacing the literal {id} and {version}
                // placeholders, so they must not be percent encoded.
                resources.Should().ContainSingle(e => e["@type"].ToString() == "PackageDetailsUriTemplate/5.1.0")
                    .Which["@id"].ToString().Should().Be("https://example.com/feed/index.html#package/{id}/{version}");
            }
        }

        [Fact]
        public async Task GivenGalleryDisabledVerifyInitDoesNotRemoveAnExistingIndexHtml()
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
                var customHtml = "<html><body>custom</body></html>";
                File.WriteAllText(galleryOutput.FullName, customHtml);

                // Act
                var success = await InitCommand.InitAsync(settings, fileSystem, feedSettings, log, CancellationToken.None);

                // Assert
                // Sleet only ever creates the gallery. An existing file may be a custom
                // page, so disabling the gallery must not delete it.
                success.Should().BeTrue();
                galleryOutput.Refresh();
                galleryOutput.Exists.Should().BeTrue();
                File.ReadAllText(galleryOutput.FullName).Should().Be(customHtml);
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
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, log: log, token: CancellationToken.None);

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
                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, log: log, token: CancellationToken.None);

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