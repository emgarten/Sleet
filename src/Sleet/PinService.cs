using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class PinService : ISleetService
    {
        public PinService(SleetContext context)
        {

        }

        public Task AddPackage(PackageInput packageInput)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemovePackage(PackageIdentity package)
        {
            throw new NotImplementedException();
        }

        public Task<List<PackageIdentity>> GetEntries()
        {
            throw new NotImplementedException();
        }

        public Task<List<PackageIdentity>> GetEntries(string packageId)
        {
            throw new NotImplementedException();
        }
    }
}
