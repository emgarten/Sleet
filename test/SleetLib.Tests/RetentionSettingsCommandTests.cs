using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class RetentionSettingsCommandTests
    {
        [Fact]
        public async Task RetentionSettingsCommand_EnableRetention()
        {
            // Arrange
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
                        CatalogEnabled = true,
                        SymbolsEnabled = true
                    }
                };

                await InitCommand.InitAsync(context);

                // Enable retention
                var success = await RetentionSettingsCommand.RunAsync(context.LocalSettings, context.Source, 10, 5, false, log);
                var updatedSettings = await FeedSettingsUtility.GetSettingsOrDefault(context.Source, log, context.Token);

                success.Should().BeTrue();
                updatedSettings.RetentionMaxStableVersions.Should().Be(10);
                updatedSettings.RetentionMaxPrereleaseVersions.Should().Be(5);
            }
        }

        [Fact]
        public async Task RetentionSettingsCommand_DisableRetention()
        {
            // Arrange
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
                        CatalogEnabled = true,
                        SymbolsEnabled = true
                    }
                };

                await InitCommand.InitAsync(context);

                // Enable retention
                var success = await RetentionSettingsCommand.RunAsync(context.LocalSettings, context.Source, 10, 5, false, log);

                // Disable retention
                success &= await RetentionSettingsCommand.RunAsync(context.LocalSettings, context.Source, -1, -1, true, log);

                var updatedSettings = await FeedSettingsUtility.GetSettingsOrDefault(context.Source, log, context.Token);

                success.Should().BeTrue();
                updatedSettings.RetentionMaxStableVersions.Should().BeNull();
                updatedSettings.RetentionMaxPrereleaseVersions.Should().BeNull();
            }
        }
    }
}