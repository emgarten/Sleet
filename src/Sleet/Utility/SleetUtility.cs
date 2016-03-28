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
        /// Add a package to all services.
        /// </summary>
        public static async Task AddPackage(SleetContext context, PackageInput package)
        {
            var services = GetServices(context);

            foreach (var service in services)
            {
                await service.AddPackage(package);
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
                await service.RemovePackage(package);
            }
        }

        public static IReadOnlyList<ISleetService> GetServices(SleetContext context)
        {
            // Order is important here
            // Packages must be added to flat container, then the catalog, then registrations.
            return new List<ISleetService>()
            {
                new FlatContainer(context),
                new Catalog(context),
                new Registrations(context),
                new AutoComplete(context),
                new Search(context),
                new PackageIndex(context),
            };
        }
    }
}
