using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    public static class DeleteCommand
    {
        /// <summary>
        /// Lock the feed and delete a single package or all packages of a given id if no version is provided.
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="version">If empty all versions will be deleted.</param>
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, string packageId, string version, string reason, bool force, ILogger log)
        {
            var success = true;

            var token = CancellationToken.None;

            log.LogMinimal($"Reading feed {source.BaseURI.AbsoluteUri}");

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Delete", log, token))
            {
                // Validate source
                await SourceUtility.ValidateFeedForClient(source, log, token);

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

                var packages = new HashSet<PackageIdentity>();

                if (!string.IsNullOrEmpty(version))
                {
                    // Delete a single version of the package
                    var packageVersion = NuGetVersion.Parse(version);

                    packages.Add(new PackageIdentity(packageId, packageVersion));
                }
                else
                {
                    // Delete all versions of the package
                    packages.UnionWith(await existingPackageSets.Packages.GetPackagesByIdAsync(packageId));
                    packages.UnionWith(await existingPackageSets.Symbols.GetPackagesByIdAsync(packageId));
                }

                if (string.IsNullOrEmpty(reason))
                {
                    reason = string.Empty;
                }

                await RemovePackages(force, log, context, existingPackageSets, packages);

                // Save all
                log.LogMinimal($"Committing changes to {source.BaseURI.AbsoluteUri}");

                success &= await source.Commit(log, token);
            }

            if (success)
            {
                log.LogMinimal($"Successfully deleted packages.");
            }
            else
            {
                log.LogError($"Failed to delete packages.");
            }

            return success;
        }

        /// <summary>
        /// Lock the feed and delete all packages given.
        /// </summary>
        public static async Task<bool> DeletePackagesAsync(LocalSettings settings, ISleetFileSystem source, IEnumerable<PackageIdentity> packagesToDelete, string reason, bool force, ILogger log)
        {
            var success = true;

            var token = CancellationToken.None;

            log.LogMinimal($"Reading feed {source.BaseURI.AbsoluteUri}");

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Delete", log, token))
            {
                // Validate source
                await SourceUtility.ValidateFeedForClient(source, log, token);

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

                var packages = new HashSet<PackageIdentity>(packagesToDelete);

                if (string.IsNullOrEmpty(reason))
                {
                    reason = string.Empty;
                }

                await RemovePackages(force, log, context, existingPackageSets, packages);

                // Save all
                log.LogMinimal($"Committing changes to {source.BaseURI.AbsoluteUri}");

                success &= await source.Commit(log, token);
            }

            if (success)
            {
                log.LogMinimal($"Successfully deleted packages.");
            }
            else
            {
                log.LogError($"Failed to delete packages.");
            }

            return success;
        }

        private static async Task RemovePackages(bool force, ILogger log, SleetContext context, PackageSets existingPackageSets, HashSet<PackageIdentity> packages)
        {
            var toRemove = new HashSet<PackageIdentity>();
            var toRemoveSymbols = new HashSet<PackageIdentity>();

            foreach (var package in packages)
            {
                var exists = existingPackageSets.Packages.Exists(package);
                var symbolsExists = existingPackageSets.Symbols.Exists(package);

                if (!exists && !symbolsExists)
                {
                    log.LogInformation($"{package.ToString()} does not exist.");

                    if (force)
                    {
                        // ignore failures
                        continue;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Package does not exists: {package.ToString()}");
                    }
                }

                if (exists)
                {
                    toRemove.Add(package);
                }

                if (symbolsExists)
                {
                    toRemoveSymbols.Add(package);
                }

                var message = $"Removing {package.ToString()}";

                if (exists && symbolsExists)
                {
                    message = $"Removing {package.ToString()} and symbols package for {package.ToString()}";
                }
                else if (symbolsExists)
                {
                    message = $"Removing symbols package {package.ToString()}";
                }

                await log.LogAsync(LogLevel.Information, message);
            }

            // Update feed
            await log.LogAsync(LogLevel.Information, "Removing packages from feed locally");

            // Add/Remove packages
            var changeContext = SleetOperations.CreateDelete(existingPackageSets, toRemove, toRemoveSymbols);
            await SleetUtility.ApplyPackageChangesAsync(context, changeContext);
        }
    }
}