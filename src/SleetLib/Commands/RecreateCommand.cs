using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace Sleet
{
    /// <summary>
    /// Destroy and rebuild the feed.
    /// </summary>
    public static class RecreateCommand
    {
        public static Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, string? tmpPath, bool force, ILogger log)
        {
            return RunAsync(settings, source, tmpPath, force, setSettings: [], log: log);
        }

        /// <summary>
        /// Recreate the feed. setSettings are feed settings in the form {key}:{value} to apply to the new feed.
        /// </summary>
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, string? tmpPath, bool force, IEnumerable<string> setSettings, ILogger log)
        {
            // Check the settings before the feed is read or changed
            var values = FeedSettingsUtility.ParseAndValidateSettingValues(setSettings);

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

                // Read settings and persist them in the new feed, with the values passed in applied.
                var feedSettings = await FeedSettingsUtility.GetSettingsOrDefault(source, log, token);

                if (values.Count > 0)
                {
                    feedSettings = FeedSettingsUtility.ApplySettings(feedSettings, values);
                }

                // Check before the nupkg folder is created, a blocked recreate must not clean up --nupkg-path.
                // Run every check so that all packages that would be lost are reported together.
                var symbolsCheck = await CheckSymbolsPackagesAsync(settings, source, feedSettings, force, log, token);
                var unindexedCheck = await CheckUnindexedPackagesAsync(settings, source, feedSettings, force, log, token);

                if (!symbolsCheck || !unindexedCheck)
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

        /// <summary>
        /// Recreate only backs up the packages listed in sleet.packageindex.json. Any other .nupkg files in the feed,
        /// for example every package when the index is missing, would be deleted without a backup.
        /// Block unless force is used.
        /// </summary>
        private static async Task<bool> CheckUnindexedPackagesAsync(LocalSettings settings, ISleetFileSystem source, FeedSettings feedSettings, bool force, ILogger log, CancellationToken token)
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

            var indexedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            indexedPaths.UnionWith(packageSets.Packages.Index.Select(e => FlatContainer.GetNupkgPath(context, e).AbsoluteUri));
            indexedPaths.UnionWith(packageSets.Symbols.Index.Select(e => GetSymbolsNupkgPath(source, e).AbsoluteUri));

            var unindexedPaths = (await source.GetFiles(log, token))
                .Select(e => e.EntityUri.AbsoluteUri)
                .Where(e => e.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) && !indexedPaths.Contains(e))
                .OrderBy(e => e, StringComparer.Ordinal)
                .ToList();

            if (unindexedPaths.Count < 1)
            {
                return true;
            }

            if (force)
            {
                log.LogWarning($"Removing {unindexedPaths.Count} .nupkg files that are not in sleet.packageindex.json.");
            }
            else
            {
                log.LogError($"The feed contains {unindexedPaths.Count} .nupkg files that are not in sleet.packageindex.json. Recreating the feed would delete them without a backup. Copy them out of the feed and push them again before running recreate, or use --force to delete them.");
            }

            var level = force ? LogLevel.Warning : LogLevel.Error;

            foreach (var path in unindexedPaths)
            {
                log.Log(level, path);
            }

            // With force the files are deleted along with the rest of the feed.
            return force;
        }

        /// <summary>
        /// Path of a .symbols.nupkg stored by the removed symbols server.
        /// </summary>
        private static Uri GetSymbolsNupkgPath(ISleetFileSystem source, PackageIdentity package)
        {
            var version = package.Version.ToNormalizedString();

            return source.GetPath($"/symbols/packages/{package.Id}/{version}/{package.Id}.{version}.symbols.nupkg".ToLowerInvariant());
        }
    }
}
