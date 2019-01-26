using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

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

        public static async Task ApplyPackageChangesAsync(SleetContext context, SleetChangeContext changeContext)
        {
            var toAdd = changeContext.ToAdd;
            var toRemove = changeContext.ToRemove;
            var packageIndexBeforeChanges = changeContext.OriginalIndex;
            var updatedIndex = changeContext.UpdatedIndex;

            var tasks = new List<Task>();

            var catalog = GetCatalogService(context);
            var catalogTask = ApplyAddRemoveAsync(catalog, toAdd, toRemove);
            tasks.Add(catalogTask);

            if (context.SourceSettings.SymbolsEnabled)
            {
                var symbols = new Symbols(context);
                tasks.Add(ApplyAddRemoveSymbolsAsync(symbols, toAdd, toRemove));
            }

            var flatContainer = new FlatContainer(context);
            tasks.Add(flatContainer.ApplyChangesAsync(changeContext));

            var autoComplete = new AutoComplete(context);
            tasks.Add(autoComplete.CreateAsync(updatedIndex.Packages.Index.Select(e => e.Id)));

            var packageIndex = new PackageIndex(context);
            tasks.Add(packageIndex.CreateAsync(updatedIndex));


            var registrations = new Registrations(context);
            var search = new Search(context);

            // Wait for the catalog task to complete before updating registrations
            await catalogTask;

            // Update registations
            await ApplyAddRemoveAsync(registrations, toAdd, toRemove);

            // Search depends on registrations
            tasks.Add(search.ApplyChangesAsync(changeContext));


            // Wait for all the first set of services to complete.
            // Services after this have requirements on the outputs of these services.
            await Task.WhenAll(tasks);
        }

        private static async Task ApplyAddRemoveAsync(IAddRemovePackages service, List<PackageInput> toAdd, List<PackageInput> toRemove)
        {
            // Remove packages
            await service.RemovePackagesAsync(toRemove.Where(e => !e.IsSymbolsPackage).Select(e => e.Identity).ToList());

            // Add
            await service.AddPackagesAsync(toAdd.Where(e => !e.IsSymbolsPackage).ToList());
        }

        private static async Task ApplyAddRemoveSymbolsAsync(Symbols service, List<PackageInput> toAdd, List<PackageInput> toRemove)
        {
            // Remove packages
            foreach (var package in toRemove)
            {
                if (package.IsSymbolsPackage)
                {
                    await service.RemoveSymbolsPackageAsync(package.Identity);
                }
                else
                {
                    await service.RemovePackageAsync(package.Identity);
                }
            }

            // Add
            foreach (var package in toAdd)
            {
                if (package.IsSymbolsPackage)
                {
                    await service.AddSymbolsPackageAsync(package);
                }
                else
                {
                    await service.AddPackageAsync(package);
                }
            }
        }

        private static IAddRemovePackages GetCatalogService(SleetContext context)
        {
            IAddRemovePackages catalog = null;
            if (context.SourceSettings.CatalogEnabled)
            {
                // Full catalog that is written to the feed
                catalog = new Catalog(context);
            }
            else
            {
                // In memory catalog that is not written to the feed
                catalog = new VirtualCatalog(context);
            }

            return catalog;
        }

        /// <summary>
        /// Remove both the symbols and non-symbols package from all services.
        /// </summary>
        public static async Task RemovePackages(SleetContext context, IEnumerable<PackageIdentity> packages)
        {
            var packageIndex = new PackageIndex(context);
            var originalIndex = await packageIndex.GetPackageSetsAsync();

            var toDelete = new HashSet<PackageIdentity>(packages.Where(e => originalIndex.Packages.Index.Contains(e)));
            var toDeleteSymbols = new HashSet<PackageIdentity>(packages.Where(e => originalIndex.Symbols.Index.Contains(e)));

            var changes = SleetChangeContext.CreateDelete(originalIndex, toDelete, toDeleteSymbols);
            await ApplyPackageChangesAsync(context, changes);
        }

        /// <summary>
        /// Remove both the symbols and non-symbols package from all services.
        /// </summary>
        public static Task RemovePackage(SleetContext context, PackageIdentity package)
        {
            return RemovePackages(context, new[] { package });
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
            var changes = await SleetChangeContext.CreateDeleteAsync(context, packages);
            await ApplyPackageChangesAsync(context, changes);
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
            var changes = await SleetChangeContext.CreateDeleteAsync(context, new List<PackageIdentity>(), packages);
            await ApplyPackageChangesAsync(context, changes);
        }
    }
}