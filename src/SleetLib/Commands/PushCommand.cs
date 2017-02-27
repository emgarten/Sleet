using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;

namespace Sleet
{
    public static class PushCommand
    {
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, List<string> inputs, bool force, bool skipExisting, ILogger log)
        {
            var token = CancellationToken.None;

            log.LogMinimal($"Reading feed {source.BaseURI.AbsoluteUri}");

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
                // Get packages
                packages.AddRange(GetPackageInputs(inputs, now, log));

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

                log.LogInformation("Reading existing package index");

                var packageIndex = new PackageIndex(context);

                foreach (var package in packages)
                {
                    var packageString = $"{package.Identity.Id} {package.Identity.Version.ToFullString()}";

                    log.LogMinimal($"Pushing {packageString}");

                    log.LogInformation($"Checking if package exists.");

                    if (await packageIndex.Exists(package.Identity))
                    {
                        if (skipExisting)
                        {
                            log.LogMinimal($"Package already exists, skipping {packageString}");
                            continue;
                        }
                        else if (force)
                        {
                            log.LogInformation($"Package already exists, removing {packageString}");
                            await SleetUtility.RemovePackage(context, package.Identity);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Package already exists: {packageString}.");
                        }
                    }

                    log.LogInformation($"Adding {packageString}");
                    await SleetUtility.AddPackage(context, package);
                }

                // Save all
                log.LogMinimal($"Committing changes to {source.BaseURI.AbsoluteUri}");

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
                log.LogMinimal("Successfully pushed packages.");
            }
            else
            {
                log.LogError("Failed to push packages.");
            }

            return exitCode;
        }

        /// <summary>
        /// Parse input arguments for nupkg paths.
        /// </summary>
        public static List<PackageInput> GetPackageInputs(List<string> inputs, DateTimeOffset now, ILogger log)
        {
            var packages = new List<PackageInput>();

            // Check inputs
            if (inputs.Count < 1)
            {
                throw new ArgumentException("No packages found.");
            }

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var input in inputs)
            {
                var inputFile = Path.GetFullPath(input);

                if (File.Exists(inputFile))
                {
                    files.Add(inputFile);
                }
                else if (Directory.Exists(inputFile))
                {
                    var directoryFiles = Directory.GetFiles(inputFile, "*.nupkg", SearchOption.AllDirectories)
                        .Where(file => file.IndexOf(".symbols.nupkg") < 0)
                        .ToList();

                    if (directoryFiles.Count < 1)
                    {
                        throw new FileNotFoundException($"Unable to find nupkgs in '{inputFile}'.");
                    }

                    files.UnionWith(directoryFiles);
                }
                else
                {
                    throw new FileNotFoundException($"Unable to find '{inputFile}'.");
                }
            }

            if (files.Any(file => file.IndexOf(".symbols.nupkg") > -1))
            {
                throw new ArgumentException("Symbol packages are not supported.");
            }

            foreach (var file in files)
            {
                // Validate package
                PackageInput packageInput = null;

                log.LogInformation($"Reading {file}");

                try
                {
                    var zip = new ZipArchive(File.OpenRead(file), ZipArchiveMode.Read, leaveOpen: false);

                    var package = new PackageArchiveReader(zip);

                    packageInput = new PackageInput()
                    {
                        PackagePath = file,
                        Identity = package.GetIdentity(),
                        Package = package,
                        Zip = zip,
                    };
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

                // Check for correct nuspec name
                var nuspecName = packageInput.Identity.Id + ".nuspec";
                if (packageInput.Zip.GetEntry(nuspecName) == null)
                {
                    throw new InvalidDataException($"'{packageInput.PackagePath}' does not contain '{nuspecName}'.");
                }

                // Check for multiple nuspec files
                if (packageInput.Zip.Entries.Where(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).Count() > 1)
                {
                    throw new InvalidDataException($"'{packageInput.PackagePath}' contains multiple nuspecs and cannot be consumed.");
                }

                // Check for duplicates
                if (packages.Any(package => package.Identity.Equals(packageInput.Identity)))
                {
                    throw new InvalidOperationException($"Duplicate packages detected for '{packageInput.Identity}'.");
                }

                packages.Add(packageInput);
            }

            return packages;
        }
    }
}