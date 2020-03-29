using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class RetentionPruneCommand
    {
        /// <summary>
        /// Run prune
        /// 1. Lock the feed
        /// 2. Verify client compat
        /// 3. Prune packges
        /// 4. Commit
        /// </summary>
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, RetentionPruneCommandContext pruneContext, ILogger log)
        {
            var exitCode = true;

            log.LogMinimal($"Pruning packages in {source.BaseURI}");
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

                exitCode = await PrunePackages(context, pruneContext);
            }

            return exitCode;
        }

        /// <summary>
        /// Prune feed packages and commit to source
        /// </summary>
        public static async Task<bool> PrunePackages(SleetContext context, RetentionPruneCommandContext pruneContext)
        {
            var exitCode = true;
            var log = context.Log;

            // Find packages to prune and remove them locally
            await PrunePackagesNoCommit(context, pruneContext);

            // Commit changes to source
            exitCode = await context.Source.Commit(log, context.Token);

            if (exitCode)
            {
                await log.LogAsync(LogLevel.Minimal, "Successfully pruned packages.");
            }
            else
            {
                await log.LogAsync(LogLevel.Error, "Failed to prune packages.");
            }

            return exitCode;
        }

        /// <summary>
        /// Prune feed packages without committing 
        /// </summary>
        public static async Task<HashSet<PackageIdentity>> PrunePackagesNoCommit(SleetContext context, RetentionPruneCommandContext pruneContext)
        {
            var packageIndex = new PackageIndex(context);
            var existingPackageSets = await packageIndex.GetPackageSetsAsync();
            var allPackages = await PruneUtility.ResolvePackageSets(existingPackageSets);

            var stableMax = pruneContext.StableVersionMax == null ? context.SourceSettings.RetentionMaxStableVersions : pruneContext.StableVersionMax;
            var prerelMax = pruneContext.PrereleaseVersionMax == null ? context.SourceSettings.RetentionMaxPrereleaseVersions : pruneContext.PrereleaseVersionMax;

            if (stableMax == null || stableMax < 1)
            {
                throw new ArgumentException("Package retention must specify a maximum number of stable versions that is > 0");
            }

            if (prerelMax == null || prerelMax < 1)
            {
                throw new ArgumentException("Package retention must specify a maximum number of prerelease versions that is > 0");
            }

            var toPrune = PruneUtility.GetPackagesToPrune(allPackages, pruneContext.PinnedPackages, (int)stableMax, (int)prerelMax);

            await RemovePackages(context, existingPackageSets, toPrune, pruneContext.DryRun, context.Log);

            return toPrune;
        }

        /// <summary>
        /// Remove packages from feed without committing
        /// </summary>
        private static async Task RemovePackages(SleetContext context, PackageSets existingPackageSets, HashSet<PackageIdentity> packagesToRemove, bool dryRun, ILogger log)
        {
            var toRemove = new HashSet<PackageIdentity>();
            var toRemoveSymbols = new HashSet<PackageIdentity>();

            foreach (var package in packagesToRemove)
            {
                var exists = existingPackageSets.Packages.Exists(package);
                var symbolsExists = existingPackageSets.Symbols.Exists(package);

                if (exists)
                {
                    toRemove.Add(package);
                }

                if (symbolsExists)
                {
                    toRemoveSymbols.Add(package);
                }

                if (exists || symbolsExists)
                {
                    await log.LogAsync(LogLevel.Information, $"Pruning {package.ToString()}");
                }
            }

            if (toRemove.Count < 1 && toRemoveSymbols.Count < 1)
            {
                await log.LogAsync(LogLevel.Information, $"No packages need pruning.");
            }
            else if (!dryRun)
            {
                // Add/Remove packages
                var changeContext = SleetOperations.CreateDelete(existingPackageSets, toRemove, toRemoveSymbols);
                await SleetUtility.ApplyPackageChangesAsync(context, changeContext);
            }
        }
    }
}