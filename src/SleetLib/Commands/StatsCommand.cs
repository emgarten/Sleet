using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public static class StatsCommand
    {
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var exitCode = true;

            log.LogMinimal($"Stats for {source.BaseURI}");

            var token = CancellationToken.None;

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Stats", log, token))
            {
                // Validate source
                await UpgradeUtility.EnsureFeedVersionMatchesTool(source, log, token);

                // Get sleet.settings.json
                var sourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(source, log, token);

                // Settings context used for all operations
                var context = new SleetContext()
                {
                    LocalSettings = settings,
                    SourceSettings = sourceSettings,
                    Log = log,
                    Source = source,
                    Token = token
                };

                var packageIndex = new PackageIndex(context);
                var existingPackageSets = await packageIndex.GetPackageSetsAsync();

                var uniqueIds = existingPackageSets.Packages.Index
                    .Concat(existingPackageSets.Symbols.Index)
                    .Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase);

                log.LogMinimal($"Packages: {existingPackageSets.Packages.Index.Count}");
                log.LogMinimal($"Symbols Packages: {existingPackageSets.Symbols.Index.Count}");
                log.LogMinimal($"Unique package ids: {uniqueIds.Count()}");
            }

            return exitCode;
        }
    }
}