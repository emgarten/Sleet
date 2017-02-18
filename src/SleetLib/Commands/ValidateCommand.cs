using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class ValidateCommand
    {
        public static async Task<bool> RunAsync(LocalSettings settings, ISleetFileSystem source, ILogger log)
        {
            var token = CancellationToken.None;

            log.LogMinimal($"Reading feed {source.BaseURI.AbsoluteUri}");

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(source, log, token))
            {
                // Validate source
                await UpgradeUtility.EnsureFeedVersionMatchesTool(source, log, token);

                return await Validate(settings, source, log, token);
            }
        }

        /// <summary>
        /// Validate packages. This does not lock or verify the version of the feed.
        /// </summary>
        public static async Task<bool> Validate(LocalSettings settings, ISleetFileSystem source, ILogger log, CancellationToken token)
        {
            var success = true;

            // Get sleet.settings.json
            var sourceSettings = new SourceSettings();

            // Settings context used for all operations
            var context = new SleetContext()
            {
                LocalSettings = settings,
                SourceSettings = sourceSettings,
                Log = log,
                Source = source,
                Token = token
            };

            // Create all services
            var catalog = new Catalog(context);
            var registrations = new Registrations(context);
            var flatContainer = new FlatContainer(context);
            var search = new Search(context);
            var autoComplete = new AutoComplete(context);
            var packageIndex = new PackageIndex(context);

            var services = new List<ISleetService>
                {
                    catalog,
                    registrations,
                    flatContainer,
                    search
                };

            // Verify against the package index
            var indexedPackages = await packageIndex.GetPackagesAsync();
            var allIndexIds = indexedPackages.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Verify auto complete
            log.LogMinimal($"Validating {autoComplete.Name}");
            var autoCompleteIds = await autoComplete.GetPackageIds();
            var missingACIds = allIndexIds.Except(autoCompleteIds).ToList();
            var extraACIds = autoCompleteIds.Except(allIndexIds).ToList();

            if (missingACIds.Count() > 0 || extraACIds.Count() > 0)
            {
                log.LogError("Missing autocomplete packages: " + string.Join(", ", missingACIds));
                log.LogError("Extra autocomplete packages: " + string.Join(", ", extraACIds));
                success = false;
            }
            else
            {
                log.LogMinimal("Autocomplete packages valid");
            }

            // Verify everything else
            foreach (var service in services)
            {
                log.LogMinimal($"Validating {service.Name}");

                var allPackagesService = service as IPackagesLookup;
                var byIdService = service as IPackageIdLookup;

                var servicePackages = new HashSet<PackageIdentity>();

                // Use get all if possible
                if (allPackagesService != null)
                {
                    servicePackages.UnionWith(await allPackagesService.GetPackagesAsync());
                }
                else if (byIdService != null)
                {
                    foreach (var id in allIndexIds)
                    {
                        servicePackages.UnionWith(await byIdService.GetPackagesByIdAsync(id));
                    }
                }
                else
                {
                    log.LogError($"Unable to get packages for {service.Name}");
                    continue;
                }

                var diff = new PackageDiff(indexedPackages, servicePackages);

                if (diff.HasErrors)
                {
                    log.LogError(diff.ToString());

                    success = false;
                }
                else
                {
                    log.LogMinimal(diff.ToString());
                    log.LogMinimal($"{service.Name} packages valid");
                }
            }

            if (success)
            {
                log.LogMinimal($"Feed valid");
            }
            else
            {
                log.LogError($"Feed invalid!");
            }

            return success;
        }

        private class PackageDiff
        {
            /// <summary>
            /// Extra packages that should not exist.
            /// </summary>
            public HashSet<PackageIdentity> Extra { get; } = new HashSet<PackageIdentity>();

            /// <summary>
            /// Packages expected to exist that are missing.
            /// </summary>
            public HashSet<PackageIdentity> Missing { get; } = new HashSet<PackageIdentity>();

            public PackageDiff(IEnumerable<PackageIdentity> expected, IEnumerable<PackageIdentity> actual)
            {
                Extra.UnionWith(actual.Except(expected));
                Missing.UnionWith(expected.Except(actual));
            }

            public bool HasErrors
            {
                get
                {
                    return Extra.Count > 0 || Missing.Count > 0;
                }
            }

            public override string ToString()
            {
                var sb = new StringBuilder();

                sb.AppendLine($"Missing packages: {Missing.Count}");

                foreach (var package in Missing.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Version))
                {
                    sb.AppendLine($"  {package.Id} {package.Version.ToFullVersionString()}");
                }

                sb.AppendLine($"Extra packages: {Extra.Count}");

                foreach (var package in Extra.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Version))
                {
                    sb.AppendLine($"  {package.Id} {package.Version.ToFullVersionString()}");
                }

                return sb.ToString().TrimEnd();
            }
        }
    }
}