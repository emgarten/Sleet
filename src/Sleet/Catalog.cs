using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    public class Catalog : ISleetService
    {
        public Catalog(SleetContext context)
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

        public Task<List<CatalogEntry>> GetEntries()
        {
            throw new NotImplementedException();
        }

        public Task<List<CatalogEntry>> GetEntries(string packageId)
        {
            throw new NotImplementedException();
        }

        public Task<CatalogEntry> GetEntry(PackageIdentity packageIdentity)
        {
            throw new NotImplementedException();
        }

        public JObject CreatePackageDetails(Uri iri, PackageInput packageInput)
        {
            throw new NotImplementedException();
        }
    }
}
