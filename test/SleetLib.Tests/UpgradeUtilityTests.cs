using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class UpgradeUtilityTests
    {
        [Fact]
        public async Task UpgradeUtility_Verify220FeedsAreCompatibleAsync()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
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
                    {
                        CatalogEnabled = true
                    }
                };

                // Init
                await InitCommand.InitAsync(context);

                // Change index.json
                var indexJsonPath = Path.Combine(target.Root, "index.json");
                var json = JObject.Parse(File.ReadAllText(indexJsonPath));
                json["sleet:version"] = "2.2.1";
                File.WriteAllText(indexJsonPath, json.ToString());

                // Verify no exceptions
                var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                await UpgradeUtility.EnsureCompatibility(fileSystem2, log, CancellationToken.None);
            }
        }

        [Fact]
        public async Task UpgradeUtility_Verify210FeedsAreNotCompatibleAsync()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
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
                    {
                        CatalogEnabled = true
                    }
                };

                // Init
                await InitCommand.InitAsync(context);

                // Change index.json
                var indexJsonPath = Path.Combine(target.Root, "index.json");
                var json = JObject.Parse(File.ReadAllText(indexJsonPath));
                json["sleet:version"] = "2.1.1";
                File.WriteAllText(indexJsonPath, json.ToString());

                Exception ex = null;
                try
                {
                    var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                    await UpgradeUtility.EnsureCompatibility(fileSystem2, log, CancellationToken.None);
                }
                catch (Exception current)
                {
                    ex = current;
                }

                ex.Should().NotBeNull();
                ex.Message.Should().Contain("Sleet recreate");
            }
        }

        [Fact]
        public async Task UpgradeUtility_VerifyRequiredVersionFailsForOlderVersionAsync()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
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
                    {
                        CatalogEnabled = true
                    }
                };

                // Init
                await InitCommand.InitAsync(context);

                // Change index.json
                var indexJsonPath = Path.Combine(target.Root, "index.json");
                var json = JObject.Parse(File.ReadAllText(indexJsonPath));
                json["sleet:requiredVersion"] = "4.1.0";
                File.WriteAllText(indexJsonPath, json.ToString());

                Exception ex = null;
                try
                {
                    var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                    await UpgradeUtility.EnsureCompatibility(fileSystem2, log, CancellationToken.None);
                }
                catch (Exception current)
                {
                    ex = current;
                }

                ex.Should().NotBeNull();
                ex.Message.Should().Contain("requires Sleet version: (>= 4.1.0)  Upgrade your Sleet client to work with this feed.");
            }
        }

        [Fact]
        public async Task UpgradeUtility_VerifyRequiredVersionWorksForNewerVersionAsync()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
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
                    {
                        CatalogEnabled = true
                    }
                };

                // Init
                await InitCommand.InitAsync(context);

                // Change index.json
                var indexJsonPath = Path.Combine(target.Root, "index.json");
                var json = JObject.Parse(File.ReadAllText(indexJsonPath));
                json["sleet:requiredVersion"] = "2.3.0";
                File.WriteAllText(indexJsonPath, json.ToString());

                var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                await UpgradeUtility.EnsureCompatibility(fileSystem2, log, CancellationToken.None);
            }
        }

        [Fact]
        public async Task UpgradeUtility_VerifyUnknownCapabilityFailsAsync()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
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
                    {
                        CatalogEnabled = true
                    }
                };

                // Init
                await InitCommand.InitAsync(context);

                // Change index.json
                var indexJsonPath = Path.Combine(target.Root, "index.json");
                var json = JObject.Parse(File.ReadAllText(indexJsonPath));
                json["sleet:capabilities"] = "newfeature:10.0.0";
                File.WriteAllText(indexJsonPath, json.ToString());

                Exception ex = null;
                try
                {
                    var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                    await UpgradeUtility.EnsureCompatibility(fileSystem2, log, CancellationToken.None);
                }
                catch (Exception current)
                {
                    ex = current;
                }

                ex.Should().NotBeNull();
                ex.Message.Should().Contain("requires a newer version of Sleet. Upgrade your Sleet client to work with this feed.");
            }
        }

        [Fact]
        public async Task UpgradeUtility_VerifyHigherVersionOfCapabilityFailsAsync()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
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
                    {
                        CatalogEnabled = true
                    }
                };

                // Init
                await InitCommand.InitAsync(context);

                // Change index.json
                var indexJsonPath = Path.Combine(target.Root, "index.json");
                var json = JObject.Parse(File.ReadAllText(indexJsonPath));
                json["sleet:capabilities"] = "schema:99999.9999.9999";
                File.WriteAllText(indexJsonPath, json.ToString());

                Exception ex = null;
                try
                {
                    var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                    await UpgradeUtility.EnsureCompatibility(fileSystem2, log, CancellationToken.None);
                }
                catch (Exception current)
                {
                    ex = current;
                }

                ex.Should().NotBeNull();
                ex.Message.Should().Contain("requires a newer version of Sleet. Upgrade your Sleet client to work with this feed.");
            }
        }

        [Fact]
        public async Task UpgradeUtility_VerifyLowerVersionOfCapabilityFailsAsync()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
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
                    {
                        CatalogEnabled = true
                    }
                };

                // Init
                await InitCommand.InitAsync(context);

                // Change index.json
                var indexJsonPath = Path.Combine(target.Root, "index.json");
                var json = JObject.Parse(File.ReadAllText(indexJsonPath));
                json["sleet:capabilities"] = "schema:0.0.0";
                File.WriteAllText(indexJsonPath, json.ToString());

                Exception ex = null;
                try
                {
                    var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                    await UpgradeUtility.EnsureCompatibility(fileSystem2, log, CancellationToken.None);
                }
                catch (Exception current)
                {
                    ex = current;
                }

                ex.Should().NotBeNull();
                ex.Message.Should().Contain("by running 'Sleet recreate' against this feed.");
            }
        }

        [Fact]
        public async Task UpgradeUtility_VerifyMatchingVersionOfCapabilityWorksAsync()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
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
                    {
                        CatalogEnabled = true
                    }
                };

                // Init
                await InitCommand.InitAsync(context);

                // Change index.json
                var indexJsonPath = Path.Combine(target.Root, "index.json");
                var json = JObject.Parse(File.ReadAllText(indexJsonPath));
                json["sleet:capabilities"] = "schema:1.0.0";
                File.WriteAllText(indexJsonPath, json.ToString());

                var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                await UpgradeUtility.EnsureCompatibility(fileSystem2, log, CancellationToken.None);
            }
        }
    }
}
