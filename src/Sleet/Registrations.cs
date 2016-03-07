using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Logging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class Registrations : ISleetService
    {
        public Registrations(SleetContext context)
        {

        }

        public Task AddPackage(PackageInput package)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemovePackage(PackageIdentity package)
        {
            throw new NotImplementedException();
        }
    }
}
