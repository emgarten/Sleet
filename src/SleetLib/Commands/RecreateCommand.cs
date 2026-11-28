using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
                // Check before the nupkg folder is created, a blocked recreate must not clean up --nupkg-path.
                var feed = await CheckFeedAsync(settings, source, force, log, token);

                if (feed == null)
                {
                    return false;
                }

                // Persist the settings in the new feed, with the values passed in applied.
                var feedSettings = feed.Settings;

                if (values.Count > 0)
                {
                    feedSettings = FeedSettingsUtility.ApplySettings(feedSettings, values);
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
                    // With force an unreadable package index leaves nothing to download, the packages are removed.
                    if (feed.CanReadPackageIndex)
                    {
                        // Symbols packages are not supported and cannot be pushed to the new feed.
                        var downloadSuccess = await DownloadCommand.DownloadPackages(settings, source, localCache.Root.FullName, force, skipExisting: false, includeSymbols: false, sourceSettings: feed.Settings, log: log, token: token);

                        if (!force && !downloadSuccess)
                        {
                            log.LogError("Unable to recreate the feed due to errors download packages. Use --force to skip this check.");
                            return false;
                        }
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
        /// Check that the feed can be recreated before anything is changed, and report every problem together.
        /// Recreate only backs up the packages listed in sleet.packageindex.json, every other .nupkg file is deleted with the old feed.
        /// Returns null if the feed can't be recreated. With force the problems are logged as warnings, files that can't be read
        /// are replaced and packages that can't be backed up are removed.
        /// </summary>
        private static async Task<FeedCheckResult?> CheckFeedAsync(LocalSettings settings, ISleetFileSystem source, bool force, ILogger log, CancellationToken token)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Files are downloaded before they are parsed. Network and storage errors are thrown, only invalid content is reported.
            // With force index.json is replaced without reading it.
            if (!force)
            {
                var feedIndex = source.Get("index.json");
                await feedIndex.FetchAsync(log, token);

                var canReadFeedIndex = true;

                try
                {
                    await feedIndex.GetJson(log, token);
                }
                catch (Exception ex) when (IsInvalidContent(ex))
                {
                    canReadFeedIndex = false;
                    errors.Add($"- index.json can't be read: {ex.Message}");
                    errors.Add("  Use --force to replace it.");
                }

                if (canReadFeedIndex)
                {
                    // Validate source
                    await UpgradeUtility.EnsureCompatibility(source, log, token);
                }
            }

            await FeedSettingsUtility.GetSettingsFileFromFeed(source).FetchAsync(log, token);

            FeedSettings feedSettings;

            try
            {
                feedSettings = await FeedSettingsUtility.GetSettingsOrDefault(source, log, token);
            }
            catch (Exception ex) when (IsInvalidContent(ex))
            {
                feedSettings = new FeedSettings();
                errors.Add($"- sleet.settings.json can't be read: {ex.Message}");
                errors.Add("  Fix the file, or use --force to recreate the feed with the default settings.");
                warnings.Add($"sleet.settings.json can't be read, the new feed will use the default settings: {ex.Message}");
            }

            var context = new SleetContext()
            {
                LocalSettings = settings,
                SourceSettings = feedSettings,
                Log = log,
                Source = source,
                Token = token
            };

            // A missing package index is read as an empty feed
            var packageIndex = new PackageIndex(context);
            await packageIndex.FetchAsync();

            var hasPackageIndex = await packageIndex.File.Exists(log, token);
            var packageSets = new PackageSets();
            string? packageIndexError = null;

            try
            {
                packageSets = await packageIndex.GetPackageSetsAsync();
            }
            catch (Exception ex) when (IsInvalidContent(ex))
            {
                packageIndexError = ex.Message;
            }

            var nupkgPaths = (await source.GetFiles(log, token))
                .Select(e => e.EntityUri.AbsoluteUri)
                .Where(e => e.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var packagePaths = packageSets.Packages.Index.Select(e => FlatContainer.GetNupkgPath(context, e).AbsoluteUri).ToList();
            var indexedPaths = new HashSet<string>(packagePaths, StringComparer.OrdinalIgnoreCase);
            indexedPaths.UnionWith(packageSets.Symbols.Index.Select(e => GetSymbolsNupkgPath(source, e).AbsoluteUri));

            var unindexedPaths = nupkgPaths.Where(e => !indexedPaths.Contains(e)).OrderBy(e => e, StringComparer.Ordinal).ToList();
            var missingPaths = packagePaths.Where(e => !nupkgPaths.Contains(e)).OrderBy(e => e, StringComparer.Ordinal).ToList();

            if (packageIndexError != null)
            {
                errors.Add($"- sleet.packageindex.json can't be read: {packageIndexError}");
                warnings.Add($"sleet.packageindex.json can't be read: {packageIndexError}");

                if (unindexedPaths.Count > 0)
                {
                    // Push also reads the package index, the packages can only be pushed again after the feed is recreated.
                    errors.Add($"  Recreating the feed would delete the {unindexedPaths.Count} .nupkg files in the feed without a backup:");
                    errors.AddRange(unindexedPaths.Select(e => $"    {e}"));
                    errors.Add("  To keep them, copy them to a folder outside the feed, run recreate with --force, then push the folder with 'sleet push'. To delete them, use --force.");
                }
                else
                {
                    errors.Add("  Use --force to recreate the feed.");
                }
            }
            else if (unindexedPaths.Count > 0)
            {
                if (hasPackageIndex)
                {
                    errors.Add($"- The feed contains {unindexedPaths.Count} .nupkg files that are not in sleet.packageindex.json. Recreating the feed would delete them without a backup:");
                }
                else
                {
                    errors.Add($"- sleet.packageindex.json is missing. Recreating the feed would delete the {unindexedPaths.Count} .nupkg files in the feed without a backup:");
                    warnings.Add("sleet.packageindex.json is missing.");
                }

                errors.AddRange(unindexedPaths.Select(e => $"    {e}"));
                errors.Add("  To keep them, copy them to a folder outside the feed and push it with 'sleet push', then run recreate again. To delete them, use --force.");
            }

            if (unindexedPaths.Count > 0)
            {
                warnings.Add($"Removing {unindexedPaths.Count} .nupkg files that are not in sleet.packageindex.json.");
                warnings.AddRange(unindexedPaths.Select(e => $"  {e}"));
            }

            if (missingPaths.Count > 0)
            {
                errors.Add($"- {missingPaths.Count} packages in sleet.packageindex.json are missing their .nupkg files:");
                errors.AddRange(missingPaths.Select(e => $"    {e}"));
                errors.Add("  Restore the files, or use --force to remove these packages from the feed.");
                warnings.Add($"Removing {missingPaths.Count} packages from the feed because their .nupkg files are missing.");
                warnings.AddRange(missingPaths.Select(e => $"  {e}"));
            }

            // Symbols packages left from the removed symbols server can't be pushed to the new feed.
            var symbolsCount = packageSets.Symbols.Index.Count;

            if (symbolsCount > 0)
            {
                errors.Add($"- The feed contains {symbolsCount} symbols packages. Sleet no longer supports symbols packages and recreating the feed would remove them.");
                errors.Add("  Back them up with 'sleet download', then run recreate with --force.");
                warnings.Add($"Removing {symbolsCount} symbols packages from the feed.");
            }

            if (force)
            {
                foreach (var warning in warnings)
                {
                    log.LogWarning(warning);
                }
            }
            else if (errors.Count > 0)
            {
                log.LogError("The feed can't be recreated:");

                foreach (var error in errors)
                {
                    log.LogError(error);
                }

                return null;
            }

            return new FeedCheckResult(feedSettings, canReadPackageIndex: packageIndexError == null);
        }

        /// <summary>
        /// True if a downloaded feed file has invalid content.
        /// </summary>
        private static bool IsInvalidContent(Exception ex)
        {
            return ex is JsonException or InvalidDataException or InvalidCastException or ArgumentException;
        }

        /// <summary>
        /// Path of a .symbols.nupkg stored by the removed symbols server.
        /// </summary>
        private static Uri GetSymbolsNupkgPath(ISleetFileSystem source, PackageIdentity package)
        {
            var version = package.Version.ToNormalizedString();

            return source.GetPath($"/symbols/packages/{package.Id}/{version}/{package.Id}.{version}.symbols.nupkg".ToLowerInvariant());
        }

        /// <summary>
        /// Feed state read by the checks.
        /// </summary>
        private sealed class FeedCheckResult
        {
            public FeedCheckResult(FeedSettings settings, bool canReadPackageIndex)
            {
                Settings = settings;
                CanReadPackageIndex = canReadPackageIndex;
            }

            /// <summary>
            /// Settings from sleet.settings.json, or the defaults if the file can't be read.
            /// </summary>
            public FeedSettings Settings { get; }

            /// <summary>
            /// False if sleet.packageindex.json exists but can't be read. A missing file is read as an empty feed.
            /// </summary>
            public bool CanReadPackageIndex { get; }
        }
    }
}
