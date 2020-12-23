using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public static class RetentionSettingsCommand
    {

        /// <summary>
        /// Enable/Disable retention and commit
        /// </summary>
        public static Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, int stableVersionMax, int prereleaseVersionMax, bool disableRetention, ILogger log)
        {
            return RunAsync(settings, source, stableVersionMax, prereleaseVersionMax, null, disableRetention, log);
        }

        /// <summary>
        /// Enable/Disable retention and commit
        /// </summary>
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, int stableVersionMax, int prereleaseVersionMax, int? releaseLabelCount, bool disableRetention, ILogger log)
        {
            var exitCode = false;

            log.LogMinimal($"Updating package retention settings in {source.BaseURI}");
            var token = CancellationToken.None;

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Prune", log, token))
            {
                // Validate source
                await UpgradeUtility.EnsureCompatibility(source, log, token);

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

                if (disableRetention && stableVersionMax < 1 && prereleaseVersionMax < 1)
                {
                    // Remove settings
                    exitCode = await DisableRetention(context);
                }
                else if (stableVersionMax > 0 && prereleaseVersionMax > 0)
                {
                    // Add max version settings
                    exitCode = await UpdateRetentionSettings(context, stableVersionMax, prereleaseVersionMax, releaseLabelCount);
                }
            }

            return exitCode;
        }

        /// <summary>
        /// Enable package retention and update settings.
        /// </summary>
        public static async Task<bool> UpdateRetentionSettings(SleetContext context, int stableVersionMax, int prereleaseVersionMax, int? releaseLabelCount)
        {
            var exitCode = true;
            var log = context.Log;

            var feedSettings = context.SourceSettings;
            feedSettings.RetentionMaxStableVersions = stableVersionMax;
            feedSettings.RetentionMaxPrereleaseVersions = prereleaseVersionMax;
            feedSettings.RetentionGroupByFirstPrereleaseLabelCount = releaseLabelCount;

            await FeedSettingsUtility.SaveSettings(context.Source, feedSettings, log, context.Token);

            // Commit changes to source
            exitCode = await context.Source.Commit(log, context.Token);

            if (exitCode)
            {
                var releaseLabelsMessage = releaseLabelCount > 0 ? $" Group by release labels: {releaseLabelCount}" : string.Empty;

                await log.LogAsync(LogLevel.Minimal, $"Successfully updated package retention settings. Stable: {stableVersionMax} Prerelease: {prereleaseVersionMax}{releaseLabelsMessage}");

                await log.LogAsync(LogLevel.Minimal, $"Run prune to apply the new settings and remove packages from the feed.");
            }
            else
            {
                await log.LogAsync(LogLevel.Error, "Failed to update package retention settings.");
            }

            return exitCode;
        }

        /// <summary>
        /// Remove package retention settings.
        /// </summary>
        public static async Task<bool> DisableRetention(SleetContext context)
        {
            var exitCode = true;
            var log = context.Log;

            var feedSettings = context.SourceSettings;
            feedSettings.RetentionMaxStableVersions = null;
            feedSettings.RetentionMaxPrereleaseVersions = null;
            feedSettings.RetentionGroupByFirstPrereleaseLabelCount = null;

            await FeedSettingsUtility.SaveSettings(context.Source, feedSettings, log, context.Token);

            // Commit changes to source
            exitCode = await context.Source.Commit(log, context.Token);

            if (exitCode)
            {
                await log.LogAsync(LogLevel.Minimal, "Package retention disabled.");
            }
            else
            {
                await log.LogAsync(LogLevel.Error, "Failed to disable retention settings.");
            }

            return exitCode;
        }
    }
}