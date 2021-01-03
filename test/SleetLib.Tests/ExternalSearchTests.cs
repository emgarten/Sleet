using System;
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
    public class ExternalSearchTests
    {
        [Fact]
        public async Task VerifySetUpdatesIndexJson()
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
                    settings,
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: Array.Empty<string>(),
                    unsetSettings: Array.Empty<string>(),
                    setSettings: new string[] { "externalsearch:https://example.org/search/query" },
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();

                var indexJsonPath = Path.Combine(target.RootDirectory.FullName, "index.json");
                var entry = GetSearchEntry(indexJsonPath);
                var value = entry["@id"].ToObject<string>();

                value.Should().Be("https://example.org/search/query");
            }
        }

        [Fact]
        public async Task VerifyUnSetUpdatesIndexJson()
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
                    settings,
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: Array.Empty<string>(),
                    unsetSettings: Array.Empty<string>(),
                    setSettings: new string[] { "externalsearch:https://example.org/search/query" },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    settings,
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: Array.Empty<string>(),
                    unsetSettings: new string[] { "externalsearch" },
                    setSettings: Array.Empty<string>(),
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();

                var indexJsonPath = Path.Combine(target.RootDirectory.FullName, "index.json");
                var entry = GetSearchEntry(indexJsonPath);
                var value = entry["@id"].ToObject<string>();

                value.Should().NotBe("https://example.org/search/query");
            }
        }

        [Fact]
        public async Task VerifyUnSetAllUpdatesIndexJson()
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
                    settings,
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: Array.Empty<string>(),
                    unsetSettings: Array.Empty<string>(),
                    setSettings: new string[] { "externalsearch:https://example.org/search/query" },
                    log: log,
                    token: context.Token);

                success &= await FeedSettingsCommand.RunAsync(
                    settings,
                    fileSystem,
                    unsetAll: true,
                    getAll: false,
                    getSettings: Array.Empty<string>(),
                    unsetSettings: Array.Empty<string>(),
                    setSettings: Array.Empty<string>(),
                    log: log,
                    token: context.Token);

                success.Should().BeTrue();

                var indexJsonPath = Path.Combine(target.RootDirectory.FullName, "index.json");
                var entry = GetSearchEntry(indexJsonPath);
                var value = entry["@id"].ToObject<string>();

                value.Should().NotBe("https://example.org/search/query");
            }
        }

        [Fact]
        public async Task VerifyRecreateKeepsExternalSearch()
        {
            using (var tmpFolder = new TestFolder())
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
                    settings,
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: Array.Empty<string>(),
                    unsetSettings: Array.Empty<string>(),
                    setSettings: new string[] { "externalsearch:https://example.org/search/query" },
                    log: log,
                    token: context.Token);

                success &= await RecreateCommand.RunAsync(
                    settings,
                    fileSystem,
                    tmpFolder.RootDirectory.FullName,
                    false,
                    context.Log);

                success.Should().BeTrue();

                var indexJsonPath = Path.Combine(target.RootDirectory.FullName, "index.json");
                var entry = GetSearchEntry(indexJsonPath);
                var value = entry["@id"].ToObject<string>();

                value.Should().Be("https://example.org/search/query");
            }
        }

        private JObject GetSearchEntry(string indexJsonPath)
        {
            var json = JObject.Parse(File.ReadAllText(indexJsonPath));
            return GetSearchEntry(json);
        }

        private JObject GetSearchEntry(JObject serviceIndex)
        {
            var resources = (JArray)serviceIndex["resources"];
            return (JObject)resources.First(e => e["@type"].ToObject<string>().StartsWith("SearchQueryService/"));
        }
    }
}
