using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    /// <summary>
    /// A set of packages. This could be either symbols packages or non-symbols packages.
    /// </summary>
    public class PackageSet: IPackagesLookup, IAddRemovePackages
    {
        public SortedSet<PackageIdentity> Index { get; } = new SortedSet<PackageIdentity>();

        public Task<ISet<PackageIdentity>> GetPackagesAsync()
        {
            return Task.FromResult<ISet<PackageIdentity>>(Index);
        }

        public Task<ISet<PackageIdentity>> GetPackagesByIdAsync(string packageId)
        {
            var result = new SortedSet<PackageIdentity>(
                Index.Where(e => StringComparer.OrdinalIgnoreCase.Equals(e.Id, packageId)));

            return Task.FromResult<ISet<PackageIdentity>>(result);
        }

        public Task AddPackageAsync(PackageInput packageInput)
        {
            Index.Add(packageInput.Identity);

            return Task.FromResult(true);
        }

        public Task RemovePackageAsync(PackageIdentity package)
        {
            Index.Remove(package);

            return Task.FromResult(true);
        }

        public async Task AddPackagesAsync(IEnumerable<PackageInput> packageInputs)
        {
            foreach (var input in packageInputs)
            {
                await AddPackageAsync(input);
            }
        }

        public async Task RemovePackagesAsync(IEnumerable<PackageIdentity> packageInputs)
        {
            foreach (var input in packageInputs)
            {
                await RemovePackageAsync(input);
            }
        }

        public bool Exists(PackageIdentity package)
        {
            return Index.Contains(package);
        }
    }
}
