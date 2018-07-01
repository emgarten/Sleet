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
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, string packageId, string version, string reason, bool force, ILogger log)
        {
            var success = true;

            var token = CancellationToken.None;
            var now = DateTimeOffset.UtcNow;

            log.LogMinimal($"Reading feed {source.BaseURI.AbsoluteUri}");

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, log, token))
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

                var packages = new List<PackageIdentity>();

                if (!string.IsNullOrEmpty(version))
                {
                    // Delete a single version of the package
                    var packageVersion = NuGetVersion.Parse(version);

                    packages.Add(new PackageIdentity(packageId, packageVersion));
                }
                else
                {
                    // Delete all versions of the package
                    packages.AddRange(await packageIndex.GetPackagesByIdAsync(packageId));
                }

                if (string.IsNullOrEmpty(reason))
                {
                    reason = string.Empty;
                }

                foreach (var package in packages)
                {
                    var exists = await packageIndex.Exists(package);
                    var symbolsExists = await packageIndex.SymbolsExists(package);

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

                    await SleetUtility.RemovePackage(context, package);
                }

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
    }
}