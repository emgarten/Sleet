using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class SleetUtility
    {
        /// <summary>
        /// Create a dictionary of packages by id
        /// </summary>
        public static Dictionary<string, List<T>> GetPackageSetsById<T>(IEnumerable<T> packages, Func<T, string> getId)
        {
            var result = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in packages)
            {
                var id = getId(package);

                List<T> list = null;
                if (!result.TryGetValue(id, out list))
                {
                    list = new List<T>(1);
                    result.Add(id, list);
                }

                list.Add(package);
            }

            return result;
        }

        /// <summary>
        /// Add a package to all services.
        /// </summary>
        public static Task AddPackage(SleetContext context, PackageInput package)
        {
            return AddPackages(context, new[] { package });
        }

        /// <summary>
        /// Add packages to all services.
        /// Works for both symbols and non-symbol packages.
        /// </summary>
        public static async Task AddPackages(SleetContext context, IEnumerable<PackageInput> packages)
        {
            await AddNonSymbolsPackages(context, packages);
            await AddSymbolsPackages(context, packages);
        }

        /// <summary>
        /// Add packages to all services.
        /// Noops for symbols packages.
        /// </summary>
        public static async Task AddNonSymbolsPackages(SleetContext context, IEnumerable<PackageInput> packages)
        {
            var services = GetServices(context);
            var toAdd = packages.Where(e => !e.IsSymbolsPackage).ToList();

            foreach (var service in services)
            {
                await service.AddPackagesAsync(toAdd);
            }
        }

        /// <summary>
        /// Add packages to all services.
        /// Noops for non-symbols packages and when symbols is disabled.
        /// </summary>
        public static async Task AddSymbolsPackages(SleetContext context, IEnumerable<PackageInput> packages)
        {
            var symbolsEnabled = context.SourceSettings.SymbolsEnabled;

            if (symbolsEnabled)
            {
                var services = GetSymbolsServices(context);

                foreach (var package in packages)
                {
                    if (package.IsSymbolsPackage)
                    {
                        foreach (var symbolsService in services)
                        {
                            await symbolsService.AddSymbolsPackageAsync(package);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Remove both the symbols and non-symbols package from all services.
        /// Avoid removing both the symbols and non-symbols packages, this should only
        /// remove the package we are going to replace.
        /// </summary>
        public static async Task RemovePackages(SleetContext context, IEnumerable<PackageInput> packages)
        {
            var nonSymbols = packages.Where(e => !e.IsSymbolsPackage).Select(e => e.Identity).ToList();
            var symbols = packages.Where(e => e.IsSymbolsPackage).Select(e => e.Identity).ToList();

            await RemoveNonSymbolsPackages(context, nonSymbols);
            await RemoveSymbolsPackages(context, symbols);
        }

        /// <summary>
        /// Remove both the symbols and non-symbols package from all services.
        /// </summary>
        public static async Task RemovePackage(SleetContext context, PackageIdentity package)
        {
            await RemoveNonSymbolsPackage(context, package);
            await RemoveSymbolsPackage(context, package);
        }

        /// <summary>
        /// Remove a non-symbols package from all services.
        /// </summary>
        public static Task RemoveNonSymbolsPackage(SleetContext context, PackageIdentity package)
        {
            return RemoveNonSymbolsPackages(context, new[] { package });
        }

        /// <summary>
        /// Remove a non-symbols package from all services.
        /// </summary>
        public static async Task RemoveNonSymbolsPackages(SleetContext context, IEnumerable<PackageIdentity> packages)
        {
            var services = GetServices(context);

            foreach (var service in services)
            {
                await service.RemovePackagesAsync(packages);
            }
        }

        /// <summary>
        /// Remove a symbols package from all services.
        /// </summary>
        public static Task RemoveSymbolsPackage(SleetContext context, PackageIdentity package)
        {
            return RemoveSymbolsPackages(context, new[] { package });
        }

        /// <summary>
        /// Remove a symbols package from all services.
        /// </summary>
        public static async Task RemoveSymbolsPackages(SleetContext context, IEnumerable<PackageIdentity> packages)
        {
            var services = GetSymbolsServices(context);
            var symbolsEnabled = context.SourceSettings.SymbolsEnabled;

            if (symbolsEnabled)
            {
                foreach (var package in packages)
                {
                    foreach (var symbolsService in services)
                    {
                        await symbolsService.RemoveSymbolsPackageAsync(package);
                    }
                }
            }
        }

        public static IReadOnlyList<ISymbolsAddRemovePackages> GetSymbolsServices(SleetContext context)
        {
            return GetServices(context).Select(e => e as ISymbolsAddRemovePackages).Where(e => e != null).ToList();
        }

        public static IReadOnlyList<ISleetService> GetServices(SleetContext context)
        {
            // Order is important here
            // Packages must be added to flat container, then the catalog, then registrations.
            var services = new List<ISleetService>
            {
                new FlatContainer(context)
            };

            if (context.SourceSettings.CatalogEnabled)
            {
                // Catalog on disk
                services.Add(new Catalog(context));
            }
            else
            {
                // In memory catalog
                services.Add(new VirtualCatalog(context));
            }

            services.Add(new Registrations(context));
            services.Add(new AutoComplete(context));
            services.Add(new Search(context));
            services.Add(new PackageIndex(context));

            // Symbols
            if (context.SourceSettings.SymbolsEnabled)
            {
                services.Add(new Symbols(context));
            }

            return services;
        }

        /// <summary>
        /// Pre-load files in parallel
        /// </summary>
        public static Task FetchFeed(SleetContext context)
        {
            return Task.WhenAll(GetServices(context).Select(e => e.FetchAsync()));
        }
    }
}