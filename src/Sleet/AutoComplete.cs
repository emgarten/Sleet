using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class AutoComplete : ISleetService
    {
        public AutoComplete(SleetContext context)
        {

        }

        public Task AddPackage(PackageInput packageInput)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemovePackage(PackageIdentity packageIdentity)
        {
            throw new NotImplementedException();
        }
    }
}
