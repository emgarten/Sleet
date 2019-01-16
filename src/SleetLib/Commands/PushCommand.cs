using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class PushCommand
    {
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, List<string> inputs, bool force, bool skipExisting, ILogger log)
        {
            var token = CancellationToken.None;
            var now = DateTimeOffset.UtcNow;
            var packages = new List<PackageInput>();

            await log.LogAsync(LogLevel.Minimal, $"Reading feed {source.BaseURI.AbsoluteUri}");

            // Read packages before locking the feed.
            packages.AddRange(await GetPackageInputs(inputs, now, log));

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, log, token))
            {
                // Validate source
                await SourceUtility.ValidateFeedForClient(source, log, token);

                // Push
                return await PushPackages(settings, source, packages, force, skipExisting, log, token);
            }
        }

        /// <summary>
        /// Push packages to a feed.
        /// This assumes the feed is already locked.
        /// </summary>
        public static async Task<bool> PushPackages(LocalSettings settings, ISleetFileSystem source, List<string> inputs, bool force, bool skipExisting, ILogger log, CancellationToken token)
        {
            var now = DateTimeOffset.UtcNow;
            var packages = new List<PackageInput>();

            // Get packages
            packages.AddRange(await GetPackageInputs(inputs, now, log));

            // Add packages
            return await PushPackages(settings, source, packages, force, skipExisting, log, token);
        }

        /// <summary>
        /// Push packages to a feed.
        /// This assumes the feed is already locked.
        /// </summary>
        public static async Task<bool> PushPackages(LocalSettings settings, ISleetFileSystem source, List<PackageInput> packages, bool force, bool skipExisting, ILogger log, CancellationToken token)
        {
            var exitCode = true;
            var now = DateTimeOffset.UtcNow;

            // Verify no duplicate packages
            CheckForDuplicates(packages);

            // Get sleet.settings.json
            await log.LogAsync(LogLevel.Minimal, "Reading feed");
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

            // Fetch feed
            await SleetUtility.FetchFeed(context);

            await log.LogAsync(LogLevel.Information, "Reading existing package index");

            var packageIndex = new PackageIndex(context);
            await PushPackages(packages, context, packageIndex, force, skipExisting, log);

            // Save all
            await log.LogAsync(LogLevel.Minimal, $"Committing changes to {source.BaseURI.AbsoluteUri}");

            await source.Commit(log, token);

            if (exitCode)
            {
                await log.LogAsync(LogLevel.Minimal, "Successfully pushed packages.");
            }
            else
            {
                await log.LogAsync(LogLevel.Error, "Failed to push packages.");
            }

            return exitCode;
        }

        private static async Task PushPackages(List<PackageInput> packageInputs, SleetContext context, PackageIndex packageIndex, bool force, bool skipExisting, ILogger log)
        {
            var toAdd = new List<PackageInput>();
            var toRemove = new List<PackageInput>();
            var existingPackageSets = await packageIndex.GetPackageSetsAsync();

            foreach (var package in packageInputs)
            {
                var packageString = $"{package.Identity.Id} {package.Identity.Version.ToFullString()}";

                if (package.IsSymbolsPackage)
                {
                    packageString += " Symbols";

                    if (!context.SourceSettings.SymbolsEnabled)
                    {
                        await log.LogAsync(LogLevel.Warning, $"Skipping {packageString}, to push symbols packages enable the symbols server on this feed.");

                        // Skip this package
                        continue;
                    }
                }

                await log.LogAsync(LogLevel.Minimal, $"Pushing {packageString}");
                await log.LogAsync(LogLevel.Information, $"Checking if package exists.");

                var exists = false;

                if (package.IsSymbolsPackage)
                {
                    exists = existingPackageSets.Symbols.Exists(package.Identity);
                }
                else
                {
                    exists = existingPackageSets.Packages.Exists(package.Identity);
                }

                if (exists)
                {
                    if (skipExisting)
                    {
                        await log.LogAsync(LogLevel.Minimal, $"Package already exists, skipping {packageString}");
                        continue;
                    }
                    else if (force)
                    {
                        toRemove.Add(package);
                        await log.LogAsync(LogLevel.Information, $"Package already exists, removing {packageString}");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Package already exists: {packageString}.");
                    }
                }

                // Add to list of packages to push
                toAdd.Add(package);
                await log.LogAsync(LogLevel.Information, $"Adding {packageString}");
            }

            await log.LogAsync(LogLevel.Minimal, $"Syncing feed files and modifying them locally");

            // Remove conflicting packages before pushing
            await SleetUtility.RemovePackages(context, toRemove);

            // Push packages
            await SleetUtility.AddPackages(context, toAdd);
        }

        /// <summary>
        /// Parse input arguments for nupkg paths.
        /// </summary>
        private static async Task<List<PackageInput>> GetPackageInputs(List<string> inputs, DateTimeOffset now, ILogger log)
        {
            // Check inputs
            if (inputs.Count < 1)
            {
                throw new ArgumentException("No packages found.");
            }

            // Get package inputs
            var packagePaths = inputs.SelectMany(GetFiles)
                .Distinct(PathUtility.GetStringComparerBasedOnOS())
                .ToList();

            var tasks = packagePaths.Select(e => new Func<Task<PackageInput>>(() => GetPackageInput(e, log)));
            var packageInputs = await TaskUtils.RunAsync(tasks, useTaskRun: true, token: CancellationToken.None);
            var packagesSorted = packageInputs.OrderBy(e => e).ToList();

            return packagesSorted;
        }

        private static void CheckForDuplicates(List<PackageInput> packages)
        {
            PackageInput lastPackage = null;
            foreach (var package in packages.OrderBy(e => e))
            {
                if (package.Equals(lastPackage))
                {
                    throw new InvalidOperationException($"Duplicate packages detected for '{package.Identity}'.");
                }

                lastPackage = package;
            }
        }

        private static Task<PackageInput> GetPackageInput(string file, ILogger log)
        {
            // Validate package
            log.LogInformation($"Reading {file}");
            PackageInput packageInput = null;

            try
            {
                // Read basic info from the package and verify that it isn't broken.
                packageInput = PackageInput.Create(file);
            }
            catch
            {
                log.LogError($"Invalid package '{file}'.");
                throw;
            }
            
            // Display a message for non-normalized packages
            if (packageInput.Identity.Version.ToString() != packageInput.Identity.Version.ToNormalizedString())
            {
                var message = $"Package '{packageInput.PackagePath}' does not contain a normalized version. Normalized: '{packageInput.Identity.Version.ToNormalizedString()}' Nuspec version: '{packageInput.Identity.Version.ToString()}'. See https://semver.org/ for details.";
                log.LogVerbose(message);
            }

            return Task.FromResult(packageInput);
        }

        private static IEnumerable<string> GetFiles(string input)
        {
            var inputFile = Path.GetFullPath(input);

            if (File.Exists(inputFile))
            {
                yield return inputFile;
            }
            else if (Directory.Exists(inputFile))
            {
                var directoryFiles = Directory.GetFiles(inputFile, "*.nupkg", SearchOption.AllDirectories);

                if (directoryFiles.Length < 1)
                {
                    throw new FileNotFoundException($"Unable to find nupkgs in '{inputFile}'.");
                }

                foreach (var file in directoryFiles)
                {
                    yield return file;
                }
            }
            else
            {
                throw new FileNotFoundException($"Unable to find '{inputFile}'.");
            }
        }
    }
}