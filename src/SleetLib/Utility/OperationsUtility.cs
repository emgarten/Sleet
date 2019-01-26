using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sleet;

namespace Sleet
{
    public static class OperationsUtility
    {

        /// <summary>
        /// Add/Remove all non-symbols packages.
        /// </summary>
        public static async Task ApplyAddRemoveAsync(IAddRemovePackages service, List<PackageInput> toAdd, List<PackageInput> toRemove)
        {
            // Remove packages
            await service.RemovePackagesAsync(toRemove.Where(e => !e.IsSymbolsPackage).Select(e => e.Identity).ToList());

            // Add
            await service.AddPackagesAsync(toAdd.Where(e => !e.IsSymbolsPackage).ToList());
        }

        /// <summary>
        /// Add/Remove all non-symbols packages.
        /// </summary>
        public static Task ApplyAddRemoveAsync(IAddRemovePackages service, SleetOperations operations)
        {
            return ApplyAddRemoveAsync(service, operations.ToAdd, operations.ToRemove);
        }
    }
}
