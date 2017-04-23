using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class SleetUtility
    {
        /// <summary>
        /// Add a package to all services.
        /// </summary>
        public static async Task AddPackage(SleetContext context, PackageInput package)
        {
            var services = GetServices(context);

            foreach (var service in services)
            {
                await service.AddPackageAsync(package);
            }
        }

        /// <summary>
        /// Remove a package from all services.
        /// </summary>
        public static async Task RemovePackage(SleetContext context, PackageIdentity package)
        {
            var services = GetServices(context);

            foreach (var service in services)
            {
                await service.RemovePackageAsync(package);
            }
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
    }
}