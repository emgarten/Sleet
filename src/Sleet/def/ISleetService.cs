using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public interface ISleetService
    {
        /// <summary>
        /// Service name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Add a package to the service.
        /// </summary>
        Task AddPackage(PackageInput packageInput);

        /// <summary>
        /// Remove a package from the service.
        /// </summary>
        Task RemovePackage(PackageIdentity package);
    }
}
