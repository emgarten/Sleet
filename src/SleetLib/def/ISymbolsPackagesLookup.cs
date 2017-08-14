using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public interface ISymbolsPackagesLookup : ISymbolsPackageIdLookup
    {
        /// <summary>
        /// Returns all existing symbols packages.
        /// </summary>
        Task<ISet<PackageIdentity>> GetSymbolsPackagesAsync();
    }
}