using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public interface IPackagesLookup : IPackageIdLookup
    {
        /// <summary>
        /// Returns all existing packages.
        /// </summary>
        Task<ISet<PackageIdentity>> GetPackagesAsync();
    }
}