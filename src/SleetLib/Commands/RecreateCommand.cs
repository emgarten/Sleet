using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Destroy and rebuild the feed.
    /// </summary>
    public static class RecreateCommand
    {
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, string? tmpPath, bool force, ILogger log)
        {
            var success = true;
            var cleanNupkgs = true;

            var token = CancellationToken.None;

            log.LogInformation($"Reading feed {source.BaseURI.AbsoluteUri}");

            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Recreate", log, token))
            {
                if (!force)
                {
                    // Validate source
                    await UpgradeUtility.EnsureCompatibility(source, log: log, token: token);
                }

                // Read settings and persist them in the new feed.
                var feedSettings = await FeedSettingsUtility.GetSettingsOrDefault(source, log, token);

                // Check before the nupkg folder is created, a blocked recreate must not clean up --nupkg-path.
                if (!await CheckSymbolsPackagesAsync(settings, source, feedSettings, force, log, token))
                {
                    return false;
                }

                // Use the tmp path if provided
                LocalCache localCache;
                if (string.IsNullOrEmpty(tmpPath))
                {
                    localCache = new LocalCache();
                }
                else
                {
                    localCache = new LocalCache(tmpPath);
                }

                try
                {
                    // Symbols packages are not supported and cannot be pushed to the new feed.
                    var downloadSuccess = await DownloadCommand.DownloadPackages(settings, source, localCache.Root.FullName, force, skipExisting: false, includeSymbols: false, log: log, token: token);

                    if (!force && !downloadSuccess)
                    {
                        log.LogError("Unable to recreate the feed due to errors download packages. Use --force to skip this check.");
                        return false;
                    }

                    // From here on the downloaded nupkgs may be the only copy of the packages. Keep them if anything fails.
                    cleanNupkgs = false;

                    var destroySuccess = await DestroyCommand.Destroy(settings, source, log, token);

                    if (!force && !destroySuccess)
                    {
                        log.LogError("Unable to completely remove the old feed before recreating. Use --force to skip this check.");
                        return false;
                    }

                    var initSuccess = await InitCommand.InitAsync(settings, source, feedSettings, log, token);

                    if (!initSuccess)
                    {
                        log.LogError("Unable to initialize the new feed. The feed is currently broken and must be repaired manually.");
                        success = false;
                        return false;
                    }

                    // Apply settings
                    await FeedSettingsUtility.ApplySettingHandlersAsync(settings, source, feedSettings, log, token);

                    // Skip pushing for empty feeds
                    if (Directory.GetFiles(localCache.Root.FullName, "*.*", SearchOption.AllDirectories).Length > 0)
                    {
                        var pushSuccess = await PushCommand.PushPackages(settings, source, new List<string>() { localCache.Root.FullName }, force: true, skipExisting: true, log: log, token: token);

                        if (!pushSuccess)
                        {
                            log.LogError("Unable to push packages to the new feed. Try pushing the nupkgs again manually.");
                            success = false;
                            return false;
                        }
                    }

                    var validateSuccess = await ValidateCommand.Validate(settings, source, log, token);

                    if (!validateSuccess)
                    {
                        log.LogError("Something went wrong when recreating the feed. Feed validation has failed.");
                        success = false;
                        return false;
                    }

                    cleanNupkgs = true;
                }
                finally
                {
                    if (cleanNupkgs)
                    {
                        // Delete downloaded nupkgs
                        log.LogInformation($"Removing local nupkgs from {localCache.Root.FullName}");
                        localCache.Dispose();
                    }
                    else
                    {
                        var message = $"Encountered an error while recreating the feed. You may need to manually create the feed and push the nupkgs again. Nupkgs have been saved to: {localCache.Root.FullName}";

                        if (force)
                        {
                            log.LogWarning(message);
                        }
                        else
                        {
                            log.LogError(message);
                        }
                    }
                }

                log.LogMinimal("Feed recreation complete.");
            }

            return success;
        }

        /// <summary>
        /// Symbols packages left from the removed symbols server would be lost when the feed is recreated.
        /// Block unless force is used.
        /// </summary>
        private static async Task<bool> CheckSymbolsPackagesAsync(LocalSettings settings, ISleetFileSystem source, FeedSettings feedSettings, bool force, ILogger log, CancellationToken token)
        {
            var context = new SleetContext()
            {
                LocalSettings = settings,
                SourceSettings = feedSettings,
                Log = log,
                Source = source,
                Token = token
            };

            var packageSets = await new PackageIndex(context).GetPackageSetsAsync();
            var count = packageSets.Symbols.Index.Count;

            if (count > 0)
            {
                if (!force)
                {
                    log.LogError($"The feed contains {count} symbols packages. Sleet no longer supports symbols packages and recreating the feed would remove them. Back them up with 'sleet download', then run recreate with --force.");
                    return false;
                }

                log.LogWarning($"Removing {count} symbols packages from the feed.");
            }

            return true;
        }
    }
}
