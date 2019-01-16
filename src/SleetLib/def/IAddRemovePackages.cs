using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public interface IAddRemovePackages
    {
        /// <summary>
        /// Add a package to the service.
        /// </summary>
        Task AddPackageAsync(PackageInput packageInput);

        /// <summary>
        /// Add a set of packages to the service.
        /// </summary>
        Task AddPackagesAsync(IEnumerable<PackageInput> packageInputs);

        /// <summary>
        /// Remove a package from the service.
        /// </summary>
        Task RemovePackageAsync(PackageIdentity package);

        /// <summary>
        /// Remove a set of packages from the service.
        /// </summary>
        Task RemovePackagesAsync(IEnumerable<PackageIdentity> packages);
    }
}