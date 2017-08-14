using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public interface ISymbolsAddRemovePackages
    {
        /// <summary>
        /// Add a package to the service.
        /// </summary>
        Task AddSymbolsPackageAsync(PackageInput packageInput);

        /// <summary>
        /// Remove a package from the service.
        /// </summary>
        Task RemoveSymbolsPackageAsync(PackageIdentity package);
    }
}