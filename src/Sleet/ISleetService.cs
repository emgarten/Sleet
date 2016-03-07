using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public interface ISleetService
    {
        Task AddPackage(PackageInput packageInput);

        Task<bool> RemovePackage(PackageIdentity package);
    }
}
