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

            await log.LogAsync(LogLevel.Minimal, $"Reading feed {source.BaseURI.AbsoluteUri}");

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(source, log, token))
            {
                // Validate source
                await UpgradeUtility.EnsureFeedVersionMatchesTool(source, log, token);

                return await PushPackages(settings, source, inputs, force, skipExisting, log, token);
            }
        }

        public static async Task<bool> PushPackages(LocalSettings settings, ISleetFileSystem source, List<string> inputs, bool force, bool skipExisting, ILogger log, CancellationToken token)
        {
            var exitCode = true;
            var now = DateTimeOffset.UtcNow;
            var packages = new List<PackageInput>();

            try
            {
                // Get sleet.settings.json
                var settingsTask = FeedSettingsUtility.GetSettingsOrDefault(source, log, token);

                // Get packages
                packages.AddRange(GetPackageInputs(inputs, now, log));

                // Settings context used for all operations
                var context = new SleetContext()
                {
                    LocalSettings = settings,
                    SourceSettings = await settingsTask,
                    Log = log,
                    Source = source,
                    Token = token
                };

                await log.LogAsync(LogLevel.Information, "Reading existing package index");

                var packageIndex = new PackageIndex(context);

                foreach (var package in packages)
                {
                    await PushPackage(package, context, packageIndex, force, skipExisting, log);
                }

                // Save all
                await log.LogAsync(LogLevel.Minimal, $"Committing changes to {source.BaseURI.AbsoluteUri}");

                await source.Commit(log, token);
            }
            finally
            {
                // Close all zip readers
                foreach (var package in packages)
                {
                    package.Dispose();
                }
            }

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

        private static async Task PushPackage(PackageInput package, SleetContext context,PackageIndex packageIndex, bool force, bool skipExisting, ILogger log)
        {
            var packageString = $"{package.Identity.Id} {package.Identity.Version.ToFullString()}";

            if (package.IsSymbolsPackage)
            {
                packageString += " Symbols";
            }

            await log.LogAsync(LogLevel.Minimal, $"Pushing {packageString}");
            await log.LogAsync(LogLevel.Information, $"Checking if package exists.");

            var exists = false;

            if (package.IsSymbolsPackage)
            {
                exists = await packageIndex.SymbolsExists(package.Identity);
            }
            else
            {
                exists = await packageIndex.Exists(package.Identity);
            }

            if (exists)
            {
                if (skipExisting)
                {
                    await log.LogAsync(LogLevel.Minimal, $"Package already exists, skipping {packageString}");
                    return;
                }
                else if (force)
                {
                    await log.LogAsync(LogLevel.Information, $"Package already exists, removing {packageString}");
                    await SleetUtility.RemovePackage(context, package.Identity);
                }
                else
                {
                    throw new InvalidOperationException($"Package already exists: {packageString}.");
                }
            }

            await log.LogAsync(LogLevel.Information, $"Adding {packageString}");

            using (package)
            {
                package.Zip = new ZipArchive(File.OpenRead(package.PackagePath), ZipArchiveMode.Read, leaveOpen: false);
                package.Package = new PackageArchiveReader(package.Zip);

                await SleetUtility.AddPackage(context, package);
            }
        }

        /// <summary>
        /// Parse input arguments for nupkg paths.
        /// </summary>
        public static List<PackageInput> GetPackageInputs(List<string> inputs, DateTimeOffset now, ILogger log)
        {
            // Check inputs
            if (inputs.Count < 1)
            {
                throw new ArgumentException("No packages found.");
            }

            // Get package inputs
            var packages = inputs.SelectMany(GetFiles)
                .Distinct(PathUtility.GetStringComparerBasedOnOS())
                .Select(e => GetPackageInput(e, log))
                .OrderBy(e => e)
                .ToList();

            // Check for duplicates
            CheckForDuplicates(packages);

            return packages;
        }

        private static void CheckForDuplicates(List<PackageInput> packages)
        {
            PackageInput lastPackage = null;
            foreach (var package in packages)
            {
                if (package.Equals(lastPackage))
                {
                    throw new InvalidOperationException($"Duplicate packages detected for '{package.Identity}'.");
                }

                lastPackage = package;
            }
        }

        private static PackageInput GetPackageInput(string file, ILogger log)
        {
            // Validate package
            log.LogInformation($"Reading {file}");

            PackageIdentity identity = null;
            var isSymbolsPackage = false;
            var hasNuspec = false;
            var hasMultipleNuspecs = false;
            var nuspecName = string.Empty;

            try
            {
                // Read basic info from the package and verify that it isn't broken.
                using (var zip = new ZipArchive(File.OpenRead(file), ZipArchiveMode.Read, leaveOpen: false))
                using (var package = new PackageArchiveReader(zip))
                {
                    identity = package.GetIdentity();
                    isSymbolsPackage = SymbolsUtility.IsSymbolsPackage(zip, file);

                    // Check for correct nuspec name
                    nuspecName = identity.Id + ".nuspec";
                    hasNuspec = (zip.GetEntry(nuspecName) != null);
                    hasMultipleNuspecs = zip.Entries.Where(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).Count() > 1;
                }
            }
            catch
            {
                log.LogError($"Invalid package '{file}'.");
                throw;
            }

            var packageInput = new PackageInput(file, identity, isSymbolsPackage);

            // Display a message for non-normalized packages
            if (packageInput.Identity.Version.ToString() != packageInput.Identity.Version.ToNormalizedString())
            {
                var message = $"Package '{packageInput.PackagePath}' does not contain a normalized version. Normalized: '{packageInput.Identity.Version.ToNormalizedString()}' Nuspec version: '{packageInput.Identity.Version.ToString()}'. See https://semver.org/ for details.";
                log.LogVerbose(message);
            }

            // Check for correct nuspec name
            if (!hasNuspec)
            {
                throw new InvalidDataException($"'{packageInput.PackagePath}' does not contain '{nuspecName}'.");
            }

            // Check for multiple nuspec files
            if (hasMultipleNuspecs)
            {
                throw new InvalidDataException($"'{packageInput.PackagePath}' contains multiple nuspecs and cannot be consumed.");
            }

            return packageInput;
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