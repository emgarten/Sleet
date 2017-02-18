using System;
using System.Collections.Generic;
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
            using (var feedLock = await SourceUtility.VerifyInitAndLock(source, log, token))
            {
                // Validate source
                await UpgradeUtility.EnsureFeedVersionMatchesTool(source, log, token);

                // Get sleet.settings.json
                var sourceSettings = new SourceSettings();

                // Settings context used for all operations
                var context = new SleetContext()
                {
                    LocalSettings = settings,
                    SourceSettings = sourceSettings,
                    Log = log,
                    Source = source,
                    Token = token
                };

                var catalog = new Catalog(context);

                var existingEntries = await catalog.GetExistingPackagesIndexAsync();
                var packages = existingEntries.Select(e => e.PackageIdentity).ToList();
                var uniqueIds = packages.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase);

                var catalogEntries = await catalog.GetIndexEntriesAsync();

                log.LogMinimal($"Catalog entries: {catalogEntries.Count}");
                log.LogMinimal($"Packages: {existingEntries.Count}");
                log.LogMinimal($"Unique package ids: {uniqueIds.Count()}");
            }

            return exitCode;
        }
    }
}