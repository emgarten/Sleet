using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public interface ISymbolsPackageIdLookup
    {
        /// <summary>
        /// Find all existing symbols packages with the given id.
        /// </summary>
        Task<ISet<PackageIdentity>> GetSymbolsPackagesByIdAsync(string packageId);
    }
}