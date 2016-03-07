using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class FlatContainer : ISleetService
    {
        public FlatContainer(SleetContext context)
        {

        }

        /// <returns>Nupkg download url.</returns>
        public Task AddPackage(PackageInput packageInput)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemovePackage(PackageIdentity package)
        {
            throw new NotImplementedException();
        }
    }
}
