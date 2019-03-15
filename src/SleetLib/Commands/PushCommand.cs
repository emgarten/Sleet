using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public static class PushCommand
    {
        public const int DefaultBatchSize = 4096;

        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, List<string> inputs, bool force, bool skipExisting, ILogger log)
        {
            var token = CancellationToken.None;
            var now = DateTimeOffset.UtcNow;
            var success = false;
            var perfTracker = source.LocalCache.PerfTracker;

            await log.LogAsync(LogLevel.Minimal, $"Reading feed {source.BaseURI.AbsoluteUri}");

            using (var timer = PerfEntryWrapper.CreateSummaryTimer("Total execution time: {0}", perfTracker))
            {
                // Partition package inputs to avoid reading 100K nuspecs at the same time.
                var packagePaths = GetPackagePaths(inputs);
                var inputBatches = packagePaths.Partition(DefaultBatchSize);
                ISleetFileSystemLock feedLock = null;

                try
                {
                    for (var i = 0; i < inputBatches.Count; i++)
                    {
                        var inputBatch = inputBatches[i];
                        if (inputBatches.Count > 1)
                        {
                            await log.LogAsync(LogLevel.Minimal, $"Pushing {inputBatch.Count} packages. Batch: {i+1} / {inputBatches.Count}");
                        }

                        // Read packages before locking the feed the first time.
                        var packages = new List<PackageInput>(await GetPackageInputs(inputBatch, now, perfTracker, log));

                        if (feedLock == null)
                        {
                            string lockMessage = null;
                            if (packages.Count > 0)
                            {
                                lockMessage = $"Push of {packages[0].Identity.ToString()}";
                            }

                            // Check if already initialized
                            feedLock = await SourceUtility.VerifyInitAndLock(settings, source, lockMessage, log, token);

                            // Validate source
                            await SourceUtility.ValidateFeedForClient(source, log, token);
                        }

                        // Push
                        success = await PushPackages(settings, source, packages, force, skipExisting, log, token);
                    }
                }
                finally
                {
                    // Unlock the feed
                    feedLock?.Dispose();
                }
            }

            // Write out perf summary
            await perfTracker.LogSummary(log);

            return success;
        }

        /// <summary>
        /// Push packages to a feed.
        /// This assumes the feed is already locked.
        /// </summary>
        public static async Task<bool> PushPackages(LocalSettings settings, ISleetFileSystem source, List<string> inputs, bool force, bool skipExisting, ILogger log, CancellationToken token)
        {
            var now = DateTimeOffset.UtcNow;
            var success = true;

            var packagePaths = GetPackagePaths(inputs);

            // Partition input files to avoid reading 100K nuspec files at once.
            foreach (var inputSegment in packagePaths.Partition(DefaultBatchSize))
            {
                var packages = new List<PackageInput>();

                // Get packages
                packages.AddRange(await GetPackageInputs(inputSegment, now, source.LocalCache.PerfTracker, log));

                // Add packages
                success &= await PushPackages(settings, source, packages, force, skipExisting, log, token);
            }

            return success;
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
                Token = token,
                PerfTracker = source.LocalCache.PerfTracker
            };

            await log.LogAsync(LogLevel.Verbose, "Reading existing package index");

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
                        await log.LogAsync(LogLevel.Minimal, $"Skip exisiting package: {packageString}");
                        continue;
                    }
                    else if (force)
                    {
                        toRemove.Add(package);
                        await log.LogAsync(LogLevel.Information, $"Replace existing package: {packageString}");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Package already exists: {packageString}.");
                    }
                }
                else
                {
                    await log.LogAsync(LogLevel.Minimal, $"Add new package: {packageString}");
                }

                // Add to list of packages to push
                toAdd.Add(package);
            }

            await log.LogAsync(LogLevel.Minimal, $"Processing feed changes");

            // Add/Remove packages
            var changeContext = SleetOperations.Create(existingPackageSets, toAdd, toRemove);
            await SleetUtility.ApplyPackageChangesAsync(context, changeContext);
        }

        /// <summary>
        /// Parse input arguments for nupkg paths.
        /// </summary>
        private static async Task<List<PackageInput>> GetPackageInputs(List<string> packagePaths, DateTimeOffset now, IPerfTracker perfTracker, ILogger log)
        {
            using (var timer = PerfEntryWrapper.CreateSummaryTimer("Loaded package nuspecs in {0}", perfTracker))
            {
                var tasks = packagePaths.Select(e => new Func<Task<PackageInput>>(() => GetPackageInput(e, log)));
                var packageInputs = await TaskUtils.RunAsync(tasks, useTaskRun: true, token: CancellationToken.None);
                var packagesSorted = packageInputs.OrderBy(e => e).ToList();
                return packagesSorted;
            }
        }

        private static List<string> GetPackagePaths(List<string> inputs)
        {
            // Check inputs
            if (inputs.Count < 1)
            {
                throw new ArgumentException("No packages found.");
            }

            // Get package inputs
            return inputs.SelectMany(GetFiles)
               .Distinct(PathUtility.GetStringComparerBasedOnOS())
               .ToList();
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
            log.LogVerbose($"Reading {file}");
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