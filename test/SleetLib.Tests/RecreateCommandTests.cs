using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class RecreateCommandTests
    {
        [Fact]
        public async Task GivenThatTheFeedIsNotInitializedVerifyRecreateFails()
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

                Func<Task> action = async () => await RecreateCommand.RunAsync(settings, fileSystem, outputFolder, force: false, log: log);

                await action.Should().ThrowAsync<InvalidOperationException>("the feed is not initialized");
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasPackagesVerifyRecreateSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var cache3 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                var packageB = new TestNupkg("b", "2.0.0-beta+blah");
                packageA.Save(packagesFolder.Root);
                packageB.Save(packagesFolder.Root);

                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                // Recreate
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder, false, log);

                success.Should().BeTrue();

                var fileSystem3 = new PhysicalFileSystem(cache3, UriUtility.CreateUri(target.Root));

                var finalPackages = (await fileSystem3.GetFiles(log, CancellationToken.None))
                                    .Select(e => Path.GetFileName(e.EntityUri.LocalPath))
                                    .Where(e => e.EndsWith(".nupkg"))
                                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase);

                Directory.Exists(outputFolder).Should().BeFalse();

                finalPackages.Should().BeEquivalentTo(new string[] { "a.1.0.0.nupkg", "b.2.0.0-beta.nupkg" });
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasNoPackagesVerifyRecreateSucceeds()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var cache3 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);

                // Recreate
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder, false, log);

                success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasMissingPackagesVerifyRecreateFails()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var cache3 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                var packageB = new TestNupkg("b", "2.0.0-beta+blah");
                packageA.Save(packagesFolder.Root);
                packageB.Save(packagesFolder.Root);

                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                var root = new DirectoryInfo(target);
                foreach (var file in root.GetFiles("a.1.0.0*", SearchOption.AllDirectories))
                {
                    // Corrupt the feed
                    file.Delete();
                }

                // Recreate
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder, false, log);

                success.Should().BeFalse();

                var fileSystem3 = new PhysicalFileSystem(cache3, UriUtility.CreateUri(target.Root));

                var finalPackages = (await fileSystem3.GetFiles(log, CancellationToken.None))
                                    .Select(e => Path.GetFileName(e.EntityUri.LocalPath))
                                    .Where(e => e.EndsWith(".nupkg"))
                                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase);

                Directory.Exists(outputFolder).Should().BeFalse();

                log.GetMessages().Should().Contain("Unable to recreate the feed due to errors download packages");
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasMissingPackagesVerifyRecreateSucceedsWithForce()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var cache3 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();

                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                var packageB = new TestNupkg("b", "2.0.0-beta+blah");
                packageA.Save(packagesFolder.Root);
                packageB.Save(packagesFolder.Root);

                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                var root = new DirectoryInfo(target);
                foreach (var file in root.GetFiles("a.1.0.0*", SearchOption.AllDirectories))
                {
                    // Corrupt the feed
                    file.Delete();
                }

                // Recreate
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder, force: true, log: log);

                success.Should().BeTrue();

                var fileSystem3 = new PhysicalFileSystem(cache3, UriUtility.CreateUri(target.Root));

                var finalPackages = (await fileSystem3.GetFiles(log, CancellationToken.None))
                                    .Select(e => Path.GetFileName(e.EntityUri.LocalPath))
                                    .Where(e => e.EndsWith(".nupkg"))
                                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase);

                Directory.Exists(outputFolder).Should().BeFalse();

                log.GetMessages().Should().Contain("Feed recreation complete.");
            }
        }

        [Fact]
        public async Task GivenDestroyFailsVerifyRecreateReturnsFalseAndKeepsDownloadedNupkgs()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                await InitCommand.RunAsync(settings, fileSystem, log);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var faultingFileSystem = new FaultInjectingFileSystem(new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root)))
                {
                    DestroyAsync = (logger, token) => Task.FromResult(false)
                };

                var success = await RecreateCommand.RunAsync(settings, faultingFileSystem, outputFolder.Root, false, log);

                success.Should().BeFalse();
                Directory.GetFiles(outputFolder.Root, "*.nupkg", SearchOption.AllDirectories).Select(Path.GetFileName).Should().Contain("a.1.0.0.nupkg");
                log.GetMessages().Should().Contain("Unable to completely remove the old feed before recreating. Use --force to skip this check.");
                log.GetMessages().Should().Contain($"Nupkgs have been saved to: {outputFolder.Root}");
            }
        }

        [Fact]
        public async Task GivenInitThrowsVerifyRecreateThrowsAndKeepsDownloadedNupkgs()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                await InitCommand.RunAsync(settings, fileSystem, log);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var inner = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var faultingFileSystem = new FaultInjectingFileSystem(inner)
                {
                    CommitAsync = async (count, logger, token) =>
                    {
                        // Commit 1 completes the destroy, commit 2 writes the new feed.
                        if (count == 2)
                        {
                            throw new IOException("Injected commit failure.");
                        }

                        return await inner.Commit(logger, token);
                    }
                };

                Func<Task> action = async () => await RecreateCommand.RunAsync(settings, faultingFileSystem, outputFolder.Root, false, log);

                await action.Should().ThrowAsync<IOException>().WithMessage("Injected commit failure.");
                Directory.GetFiles(outputFolder.Root, "*.nupkg", SearchOption.AllDirectories).Select(Path.GetFileName).Should().Contain("a.1.0.0.nupkg");
                log.GetMessages().Should().Contain($"Nupkgs have been saved to: {outputFolder.Root}");
            }
        }

        [Fact]
        public async Task GivenInitFailsVerifyRecreateReturnsFalseAndKeepsDownloadedNupkgs()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                await InitCommand.RunAsync(settings, fileSystem, log);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var inner = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var faultingFileSystem = new FaultInjectingFileSystem(inner)
                {
                    CommitAsync = async (count, logger, token) =>
                    {
                        var result = await inner.Commit(logger, token);
                        return count != 2 && result;
                    }
                };

                var success = await RecreateCommand.RunAsync(settings, faultingFileSystem, outputFolder.Root, false, log);

                success.Should().BeFalse();
                Directory.GetFiles(outputFolder.Root, "*.nupkg", SearchOption.AllDirectories).Select(Path.GetFileName).Should().Contain("a.1.0.0.nupkg");
                log.GetMessages().Should().Contain("Unable to initialize the new feed. The feed is currently broken and must be repaired manually.");
                log.GetMessages().Should().Contain($"Nupkgs have been saved to: {outputFolder.Root}");
            }
        }

        [Fact]
        public async Task GivenValidateFailsVerifyRecreateReturnsFalseAndKeepsDownloadedNupkgs()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                await InitCommand.RunAsync(settings, fileSystem, log);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var inner = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var faultingFileSystem = new FaultInjectingFileSystem(inner)
                {
                    AfterCommit = count =>
                    {
                        if (count == 3)
                        {
                            File.Delete(Path.Combine(target.Root, "autocomplete", "query"));
                            inner.Reset();
                        }
                    }
                };

                var success = await RecreateCommand.RunAsync(settings, faultingFileSystem, outputFolder.Root, false, log);

                success.Should().BeFalse();
                Directory.GetFiles(outputFolder.Root, "*.nupkg", SearchOption.AllDirectories).Select(Path.GetFileName).Should().Contain("a.1.0.0.nupkg");
                log.GetMessages().Should().Contain("Something went wrong when recreating the feed. Feed validation has failed.");
                log.GetMessages().Should().Contain($"Nupkgs have been saved to: {outputFolder.Root}");
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasLegacySymbolsPackagesVerifyRecreateFailsWithoutForce()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                packageA.Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                TestUtility.AddLegacySymbolsServer(target.Root);
                TestUtility.AddLegacySymbolsPackage(target.Root, packagesFolder.Root, "a", "1.0.0");

                // Existing content in the nupkg folder must survive a blocked recreate
                var existingFile = Path.Combine(outputFolder.Root, "backup.txt");
                File.WriteAllText(existingFile, "backup");

                // Recreate
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder, false, log);

                success.Should().BeFalse();
                log.GetMessages(NuGet.Common.LogLevel.Error).Should().Contain("The feed contains 1 symbols packages.");
                File.Exists(existingFile).Should().BeTrue();

                // The feed is unchanged
                File.Exists(Path.Combine(target.Root, "flatcontainer", "a", "1.0.0", "a.1.0.0.nupkg")).Should().BeTrue();
                File.Exists(Path.Combine(target.Root, "symbols", "packages", "a", "1.0.0", "a.1.0.0.symbols.nupkg")).Should().BeTrue();
                File.ReadAllText(Path.Combine(target.Root, "index.json")).Should().Contain("SymbolsServer/1.0.0");
            }
        }

        [Fact]
        public async Task GivenThatTheFeedHasLegacySymbolsPackagesVerifyRecreateWithForceRemovesThem()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            using (var cache3 = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);

                var packageA = new TestNupkg("a", "1.0");
                packageA.Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder }, false, false, log);

                TestUtility.AddLegacySymbolsServer(target.Root);
                TestUtility.AddLegacySymbolsPackage(target.Root, packagesFolder.Root, "a", "1.0.0");

                // Recreate
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder, true, log);

                success.Should().BeTrue();
                log.GetMessages(NuGet.Common.LogLevel.Warning).Should().Contain("Removing 1 symbols packages from the feed.");

                var fileSystem3 = new PhysicalFileSystem(cache3, UriUtility.CreateUri(target.Root));

                var finalPackages = (await fileSystem3.GetFiles(log, TestContext.Current.CancellationToken))
                                    .Select(e => Path.GetFileName(e.EntityUri.LocalPath))
                                    .Where(e => e.EndsWith(".nupkg"));

                var packageSets = await new PackageIndex(new SleetContext()
                {
                    Token = TestContext.Current.CancellationToken,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem3,
                    SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(fileSystem3, log, TestContext.Current.CancellationToken)
                }).GetPackageSetsAsync();

                finalPackages.Should().BeEquivalentTo(new string[] { "a.1.0.0.nupkg" });
                packageSets.Symbols.Index.Should().BeEmpty();
                Directory.Exists(Path.Combine(target.Root, "symbols")).Should().BeFalse();
                File.ReadAllText(Path.Combine(target.Root, "sleet.settings.json")).Should().NotContain("symbolsfeedenabled");

                // The recreated feed no longer warns about the symbols server
                var log2 = new TestLogger();
                await UpgradeUtility.EnsureCompatibility(fileSystem3, log2, TestContext.Current.CancellationToken);

                log2.GetMessages(NuGet.Common.LogLevel.Warning).Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GivenRecreateWithSettingsVerifyTheyAreAppliedToTheFeed()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var settings = new LocalSettings();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                File.Exists(Path.Combine(target.Root, "index.html")).Should().BeTrue();

                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var setSettings = new[] { "galleryenabled:false", "catalogenabled:true", "badgesenabled:true" };

                // Act
                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder.Root, false, setSettings, log);

                var settingsJson = JObject.Parse(File.ReadAllText(Path.Combine(target.Root, "sleet.settings.json")));
                var rootJson = JObject.Parse(File.ReadAllText(Path.Combine(target.Root, "index.json")));

                // Assert
                success.Should().BeTrue();

                FeedSettingsUtility.GetSettings(settingsJson).Should().BeEquivalentTo(new Dictionary<string, string>()
                {
                    { "badgesenabled", "true" },
                    { "catalogenabled", "true" },
                    { "catalogpagesize", "1024" },
                    { "galleryenabled", "false" }
                });

                // The package already on the feed is pushed again with the new settings
                File.Exists(Path.Combine(target.Root, "flatcontainer", "a", "1.0.0", "a.1.0.0.nupkg")).Should().BeTrue();
                File.Exists(Path.Combine(target.Root, "badges", "v", "a.json")).Should().BeTrue();
                Directory.GetFiles(Path.Combine(target.Root, "catalog", "data"), "*.json").Should().ContainSingle();

                File.Exists(Path.Combine(target.Root, "index.html")).Should().BeFalse();
                rootJson.ToString().Should().NotContain("PackageDetailsUriTemplate");
            }
        }

        [Fact]
        public async Task GivenRecreateWithAnInvalidSettingVerifyTheFeedIsUnchanged()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var settings = new LocalSettings();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                await InitCommand.RunAsync(settings, fileSystem, log);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);

                var settingsPath = Path.Combine(target.Root, "sleet.settings.json");
                var settingsBefore = File.ReadAllText(settingsPath);

                // Existing content in the nupkg folder must survive a rejected recreate
                var existingFile = Path.Combine(outputFolder.Root, "backup.txt");
                File.WriteAllText(existingFile, "backup");

                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));
                var setSettings = new[] { "galleryenabled:false", "galeryenabled:false" };

                // Act
                Func<Task> action = async () => await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder.Root, false, setSettings, log);

                // Assert
                await action.Should().ThrowAsync<ArgumentException>().WithMessage("Unknown feed setting 'galeryenabled'. Use one of: *");

                File.Exists(existingFile).Should().BeTrue();
                File.Exists(Path.Combine(target.Root, "flatcontainer", "a", "1.0.0", "a.1.0.0.nupkg")).Should().BeTrue();
                File.Exists(Path.Combine(target.Root, "index.html")).Should().BeTrue();
                File.ReadAllText(settingsPath).Should().Be(settingsBefore);
            }
        }

        [Fact]
        public async Task GivenAFeedFromBeforeTheGalleryVerifyRecreateAddsIt()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var outputFolder = new TestFolder())
            using (var cache = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var settings = new LocalSettings();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settingsPath = Path.Combine(target.Root, "sleet.settings.json");

                // Older versions of Sleet wrote neither the gallery nor its setting.
                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, new[] { "galleryenabled:false" }, log, TestContext.Current.CancellationToken);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);
                await FeedSettingsCommand.RunAsync(
                    settings,
                    fileSystem,
                    unsetAll: false,
                    getAll: false,
                    getSettings: new string[] { },
                    unsetSettings: new[] { "galleryenabled" },
                    setSettings: new string[] { },
                    log: log,
                    token: TestContext.Current.CancellationToken);

                FeedSettingsUtility.GetSettings(JObject.Parse(File.ReadAllText(settingsPath))).Should().NotContainKey("galleryenabled");
                File.Exists(Path.Combine(target.Root, "index.html")).Should().BeFalse();

                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                // Act
                var success = await RecreateCommand.RunAsync(settings, fileSystem2, outputFolder.Root, false, log);

                var settingsJson = JObject.Parse(File.ReadAllText(settingsPath));
                var rootJson = JObject.Parse(File.ReadAllText(Path.Combine(target.Root, "index.json")));

                // Assert
                success.Should().BeTrue();
                FeedSettingsUtility.GetSettings(settingsJson).Should().Contain("galleryenabled", "true");
                File.Exists(Path.Combine(target.Root, "index.html")).Should().BeTrue();
                rootJson.ToString().Should().Contain("PackageDetailsUriTemplate/5.1.0");
                File.Exists(Path.Combine(target.Root, "flatcontainer", "a", "1.0.0", "a.1.0.0.nupkg")).Should().BeTrue();
            }
        }
    }
}
