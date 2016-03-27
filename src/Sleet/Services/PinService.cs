using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class PinService : ISleetService, IPackagesLookup
    {
        public string Name { get; } = nameof(PinService);

        public PinService(SleetContext context)
        {

        }

        public Task AddPackage(PackageInput packageInput)
        {
            throw new NotImplementedException();
        }

        public Task RemovePackage(PackageIdentity package)
        {
            throw new NotImplementedException();
        }

        public Task<ISet<PackageIdentity>> GetPackages()
        {
            throw new NotImplementedException();
        }

        public Task<ISet<PackageIdentity>> GetPackagesById(string packageId)
        {
            throw new NotImplementedException();
        }
    }
}
