using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class SymbolsPackages : ISleetService
    {
        public string Name => nameof(SymbolsPackages);

        public Task AddPackageAsync(PackageInput packageInput)
        {
            throw new NotImplementedException();
        }

        public Task RemovePackageAsync(PackageIdentity package)
        {
            throw new NotImplementedException();
        }
    }
}
