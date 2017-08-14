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
        /// Remove a package from the service.
        /// </summary>
        Task RemovePackageAsync(PackageIdentity package);
    }
}