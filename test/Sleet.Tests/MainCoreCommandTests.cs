using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Xunit;

namespace Sleet.Tests
{
    public class MainCoreCommandTests
    {
        [Fact]
        public async Task MainCore_RunLocalFeedLifecycle_ExercisesCommands()
        {
            using (var root = new TestFolder())
            using (var packages = new TestFolder())
            using (var download = new TestFolder())
            using (var recreate = new TestFolder())
            {
                var generatedConfig = Path.Combine(root.Root, "generated");
                var feed = Path.Combine(root.Root, "feed");
                var config = Path.Combine(root.Root, "sleet.json");
                Directory.CreateDirectory(generatedConfig);
                Directory.CreateDirectory(feed);
                WriteLocalConfig(config, ("local", feed));

                var log = new TestLogger();

                (await RunAsync(log, "createconfig", "--local", "--output", generatedConfig)).Should().Be(0);
                File.Exists(Path.Combine(generatedConfig, "sleet.json")).Should().BeTrue();

                (await RunAsync(log, "init", "--config", config, "--with-catalog")).Should().Be(0);
                File.Exists(Path.Combine(feed, "index.json")).Should().BeTrue();

                new TestNupkg("a", "1.0.0").Save(packages.Root);
                new TestNupkg("a", "2.0.0").Save(packages.Root);
                new TestNupkg("b", "1.0.0-beta").Save(packages.Root);

                (await RunAsync(log, "push", "--config", config, packages.Root)).Should().Be(0);
                (await RunAsync(log, "validate", "--config", config)).Should().Be(0);
                (await RunAsync(log, "stats", "--config", config)).Should().Be(0);
                log.GetMessages().Should().Contain("Packages: 3");

                (await RunAsync(log, "feed-settings", "--config", config, "--set", "owner:test")).Should().Be(0);
                (await RunAsync(log, "feed-settings", "--config", config, "--get", "owner")).Should().Be(0);
                log.GetMessages().Should().Contain("owner : test");

                (await RunAsync(log, "retention", "settings", "--config", config, "--stable", "1", "--prerelease", "1", "--release-labels", "1")).Should().Be(0);
                (await RunAsync(log, "retention", "prune", "--config", config, "--stable", "1", "--prerelease", "1")).Should().Be(0);

                var packagesAfterPrune = await GetPackagesAsync(config, feed);
                packagesAfterPrune.Should().BeEquivalentTo(new[]
                {
                    new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                    new PackageIdentity("b", NuGetVersion.Parse("1.0.0-beta"))
                });

                (await RunAsync(log, "download", "--config", config, "--output-path", download.Root)).Should().Be(0);
                Directory.GetFiles(download.Root, "*.nupkg", SearchOption.AllDirectories)
                    .Select(Path.GetFileName)
                    .Should().BeEquivalentTo(new[] { "a.2.0.0.nupkg", "b.1.0.0-beta.nupkg" });

                (await RunAsync(log, "delete", "--config", config, "--id", "b", "--version", "1.0.0-beta")).Should().Be(0);
                var packagesAfterDelete = await GetPackagesAsync(config, feed);
                packagesAfterDelete.Should().ContainSingle().Which.Should().Be(new PackageIdentity("a", NuGetVersion.Parse("2.0.0")));

                (await RunAsync(log, "recreate", "--config", config, "--nupkg-path", recreate.Root)).Should().Be(0);
                (await RunAsync(log, "validate", "--config", config)).Should().Be(0);

                (await RunAsync(log, "destroy", "--config", config)).Should().Be(0);
                File.Exists(Path.Combine(feed, "index.json")).Should().BeFalse();
            }
        }

        [Theory]
        [InlineData("delete", "Missing required parameter --id.", "--config")]
        [InlineData("download", "Missing required parameter --output-path.", "--config")]
        [InlineData("retention settings", "Missing required parameter --stable.", "--config")]
        public async Task MainCore_MissingRequiredOptions_ReturnsOneAndLogsError(string command, string expected, string configOption)
        {
            using (var root = new TestFolder())
            {
                var feed = Path.Combine(root.Root, "feed");
                var config = Path.Combine(root.Root, "sleet.json");
                WriteLocalConfig(config, ("local", feed));
                var log = new TestLogger();
                var args = command.Split(' ').Concat(new[] { configOption, config }).ToArray();

                var exitCode = await RunAsync(log, args);

                exitCode.Should().Be(1);
                log.GetMessages().Should().Contain(expected);
            }
        }

        [Fact]
        public async Task MainCore_RetentionSettingsWithDisableAndCounts_ReturnsOneAndLogsError()
        {
            using (var root = new TestFolder())
            {
                var config = Path.Combine(root.Root, "sleet.json");
                WriteLocalConfig(config, ("local", Path.Combine(root.Root, "feed")));
                var log = new TestLogger();

                var exitCode = await RunAsync(log, "retention", "settings", "--config", config, "--disable", "--stable", "1", "--prerelease", "1");

                exitCode.Should().Be(1);
                log.GetMessages().Should().Contain("disable may not be used with stable.");
            }
        }

        [Fact]
        public async Task MainCore_CreateConfigWithProvider_CreatesS3Template()
        {
            using (var root = new TestFolder())
            {
                var log = new TestLogger();

                var exitCode = await RunAsync(log, "createconfig", "--provider", "r2", "--output", root.Root);

                exitCode.Should().Be(0);
                var source = JObject.Parse(File.ReadAllText(Path.Combine(root.Root, "sleet.json")))["sources"][0];
                source["type"].Value<string>().Should().Be("s3");
                source["provider"].Value<string>().Should().Be("r2");
            }
        }

        [Theory]
        [InlineData("provider may not be used with azure.", "--provider", "r2", "--azure")]
        [InlineData("provider may not be used with local.", "--provider", "r2", "--local")]
        [InlineData("Unknown provider 'gcs' for s3 source.", "--provider", "gcs")]
        public async Task MainCore_CreateConfigWithInvalidProvider_ReturnsOneAndLogsError(string expected, params string[] options)
        {
            using (var root = new TestFolder())
            {
                var log = new TestLogger();

                var exitCode = await RunAsync(log, new[] { "createconfig", "--output", root.Root }.Concat(options).ToArray());

                exitCode.Should().Be(1);
                log.GetMessages().Should().Contain(expected);
                File.Exists(Path.Combine(root.Root, "sleet.json")).Should().BeFalse();
            }
        }

        [Theory]
        [InlineData("retention", "settings", "--stable", "not-a-number", "--prerelease", "1")]
        [InlineData("retention", "prune", "--stable", "not-a-number")]
        public async Task MainCore_RetentionInvalidNumbers_ReturnsOneAndLogsError(params string[] commandArgs)
        {
            using (var root = new TestFolder())
            {
                var config = Path.Combine(root.Root, "sleet.json");
                WriteLocalConfig(config, ("local", Path.Combine(root.Root, "feed")));
                var log = new TestLogger();
                var initExitCode = await RunAsync(log, "init", "--config", config);
                initExitCode.Should().Be(0);

                var exitCode = await RunAsync(log, commandArgs.Concat(new[] { "--config", config }).ToArray());

                exitCode.Should().Be(1);
                log.GetMessages().Should().Contain("[System.FormatException]");
            }
        }

        [Fact]
        public async Task MainCore_ConfigPathThatDoesNotExist_ReturnsOneAndLogsError()
        {
            using (var root = new TestFolder())
            {
                var config = Path.Combine(root.Root, "missing-sleet.json");
                var log = new TestLogger();

                var exitCode = await RunAsync(log, "init", "--config", config);

                exitCode.Should().Be(1);
                log.GetMessages().Should().Contain($"Unable to find source settings. File not found '{config}'.");
            }
        }

        [Fact]
        public async Task MainCore_SingleSourceIsSelectedWhenSourceIsOmitted()
        {
            using (var root = new TestFolder())
            {
                var feed = Path.Combine(root.Root, "feed");
                var config = Path.Combine(root.Root, "sleet.json");
                WriteLocalConfig(config, ("only", feed));
                var log = new TestLogger();

                var exitCode = await RunAsync(log, "init", "--config", config);

                exitCode.Should().Be(0);
                File.Exists(Path.Combine(feed, "index.json")).Should().BeTrue();
            }
        }

        [Fact]
        public async Task MainCore_ConfigWithoutSources_ReturnsOneAndLogsMissingSources()
        {
            using (var root = new TestFolder())
            {
                var config = Path.Combine(root.Root, "sleet.json");
                File.WriteAllText(config, new JObject(new JProperty("sources", new JArray())).ToString());
                var log = new TestLogger();

                var exitCode = await RunAsync(log, "init", "--config", config);

                exitCode.Should().Be(1);
                log.GetMessages().Should().Contain("The local settings file is missing or does not contain any sources.");
            }
        }

        [Fact]
        public async Task MainCore_ConfigWithMultipleSourcesRequiresSourceOption()
        {
            using (var root = new TestFolder())
            {
                var config = Path.Combine(root.Root, "sleet.json");
                WriteLocalConfig(config, ("first", Path.Combine(root.Root, "first")), ("second", Path.Combine(root.Root, "second")));
                var log = new TestLogger();

                var exitCode = await RunAsync(log, "init", "--config", config);

                exitCode.Should().Be(1);
                log.GetMessages().Should().Contain("The local settings file contains multiple sources. Use --source to specify the feed to use.");
            }
        }

        [Fact]
        public async Task MainCore_UnknownSourceName_ReturnsOneAndLogsError()
        {
            using (var root = new TestFolder())
            {
                var config = Path.Combine(root.Root, "sleet.json");
                WriteLocalConfig(config, ("local", Path.Combine(root.Root, "feed")));
                var log = new TestLogger();

                var exitCode = await RunAsync(log, "init", "--config", config, "--source", "missing");

                exitCode.Should().Be(1);
                log.GetMessages().Should().Contain("Unable to find source. Verify that the --source parameter is correct");
            }
        }

        private static Task<int> RunAsync(TestLogger log, params string[] args)
        {
            return Program.MainCore(args, log);
        }

        private static async Task<IReadOnlyList<PackageIdentity>> GetPackagesAsync(string configPath, string feedPath)
        {
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var settings = LocalSettings.Load(configPath);
                var context = new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = new PhysicalFileSystem(cache, UriUtility.CreateUri(feedPath)),
                    SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(new PhysicalFileSystem(cache, UriUtility.CreateUri(feedPath)), log, TestContext.Current.CancellationToken)
                };
                var packageIndex = new PackageIndex(context);
                return (await packageIndex.GetPackagesAsync()).ToList();
            }
        }

        private static void WriteLocalConfig(string configPath, params (string Name, string Path)[] sources)
        {
            var json = new JObject
            {
                { "username", "test" },
                { "useremail", "test@example.com" }
            };
            var sourceArray = new JArray();
            foreach (var source in sources)
            {
                sourceArray.Add(new JObject
                {
                    { "name", source.Name },
                    { "type", "local" },
                    { "path", source.Path },
                    { "baseURI", UriUtility.CreateUri(source.Path).AbsoluteUri }
                });
            }
            json.Add("sources", sourceArray);
            File.WriteAllText(configPath, json.ToString());
        }
    }
}
