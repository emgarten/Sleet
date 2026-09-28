using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class StatsCommandTests
    {
        [Fact]
        public async Task StatsCommand_GivenEmptyFeedVerifyCountsAreZero()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: true, log: log, token: TestContext.Current.CancellationToken);

                var success = await StatsCommand.RunAsync(settings, fileSystem, log);

                success.Should().BeTrue();
                var lines = GetLogLines(log);
                lines.Should().Contain($"Stats for {fileSystem.BaseURI}");
                lines.Should().Contain("Packages: 0");
                lines.Should().Contain("Symbols Packages: 0");
                lines.Should().Contain("Unique package ids: 0");
            }
        }

        [Fact]
        public async Task StatsCommand_GivenPackagesAndSymbolsVerifyCounts()
        {
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();

                await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: true, log: log, token: TestContext.Current.CancellationToken);
                new TestNupkg("a", "1.0.0").Save(packagesFolder.Root);
                new TestNupkg("a", "2.0.0").Save(packagesFolder.Root);
                new TestNupkg("b", "1.0.0").Save(packagesFolder.Root);
                var symbols = new TestNupkg("a", "1.0.0");
                symbols.Nuspec.IsSymbolPackage = true;
                symbols.Save(packagesFolder.Root);
                var pushed = await PushCommand.RunAsync(settings, fileSystem, new List<string>() { packagesFolder.Root }, false, false, log);
                pushed.Should().BeTrue();

                var success = await StatsCommand.RunAsync(settings, fileSystem, log);

                success.Should().BeTrue();
                var lines = GetLogLines(log);
                lines.Should().Contain("Packages: 3");
                lines.Should().Contain("Symbols Packages: 1");
                lines.Should().Contain("Unique package ids: 2");
            }
        }

        private static List<string> GetLogLines(TestLogger log)
        {
            return log.GetMessages().Split('\n').Select(line => line.Trim()).ToList();
        }
    }
}
