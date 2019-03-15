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
            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Validate", log, token))
            {
                // Validate source
                await SourceUtility.ValidateFeedForClient(source, log, token);

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

            // Create all services
            var services = new List<ISleetService>();

            var registrations = new Registrations(context);
            var flatContainer = new FlatContainer(context);
            var search = new Search(context);
            var autoComplete = new AutoComplete(context);
            var packageIndex = new PackageIndex(context);

            if (context.SourceSettings.CatalogEnabled)
            {
                // Add the catalog only if it is enabled
                var catalog = new Catalog(context);
                services.Add(catalog);
            }

            services.Add(registrations);
            services.Add(flatContainer);
            services.Add(search);

            if (context.SourceSettings.SymbolsEnabled)
            {
                var symbols = new Symbols(context);
                services.Add(symbols);
            }

            // Verify against the package index
            var indexedPackages = await packageIndex.GetPackagesAsync();
            var allIndexIds = indexedPackages.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Get symbols packages from index
            var indexedSymbolsPackages = await packageIndex.GetSymbolsPackagesAsync();
            var allIndexSymbolsIds = indexedSymbolsPackages.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

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

                var validatableService = service as IValidatableService;
                if (validatableService != null)
                {
                    // Run internal validations if the service supports it.
                    var messages = await validatableService.ValidateAsync();
                    success &= messages.All(e => e.Level != LogLevel.Error);

                    foreach (var message in messages)
                    {
                        await log.LogAsync(message);
                    }
                }
                else
                {
                    var allPackagesService = service as IPackagesLookup;
                    var byIdService = service as IPackageIdLookup;

                    var allSymbolsPackagesService = service as ISymbolsPackagesLookup;
                    var symbolsByIdService = service as ISymbolsPackageIdLookup;

                    var servicePackages = new HashSet<PackageIdentity>();
                    var serviceSymbolsPackages = new HashSet<PackageIdentity>();

                    // Non-Symbols packages
                    if (allPackagesService != null)
                    {
                        // Use get all if possible
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

                    // Symbols packages
                    if (allSymbolsPackagesService != null)
                    {
                        // Use get all if possible
                        serviceSymbolsPackages.UnionWith(await allSymbolsPackagesService.GetSymbolsPackagesAsync());
                    }
                    else if (symbolsByIdService != null)
                    {
                        foreach (var id in allIndexSymbolsIds)
                        {
                            serviceSymbolsPackages.UnionWith(await symbolsByIdService.GetSymbolsPackagesByIdAsync(id));
                        }
                    }
                    else
                    {
                        // Symbols are not supported by this service
                        continue;
                    }

                    var symbolsDiff = new PackageDiff(indexedSymbolsPackages, serviceSymbolsPackages);

                    if (symbolsDiff.HasErrors)
                    {
                        log.LogError(symbolsDiff.ToString());
                        success = false;
                    }
                    else
                    {
                        log.LogMinimal(symbolsDiff.ToString());
                        log.LogMinimal($"{service.Name} symbols packages valid");
                    }
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
    }
}