using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    public class Catalog : ISleetService
    {
        private readonly SleetContext _context;

        public Catalog(SleetContext context)
        {
            _context = context;
        }

        public Task AddPackage(PackageInput packageInput)
        {
            // Create package details page

            // Add catalog page entry

            // Update packageInput catalog url

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

        public Task<bool> Exists(PackageIdentity packageIdentity)
        {
            throw new NotImplementedException();
        }

        public JObject CreatePackageDetails(PackageInput packageInput)
        {
            var now = packageInput.Now;
            var date = now.ToString("yyyy.MM.dd.HH.mm.ss");
            var package = packageInput.Package;
            var nuspec = XDocument.Load(package.GetNuspec());

            var rootUri = new Uri($"{_context.Source.Root}catalog/data/{date}/{packageInput.Identity.Id.ToLowerInvariant()}.{packageInput.Identity.Version.ToNormalizedString().ToLowerInvariant()}.json");

            var json = JsonUtility.Create(rootUri, new List<string>() { "PackageDetails", "catalog:Permalink" });

            var context = JsonUtility.GetContext("Catalog");
            json.Add("@context", context);

            json.Add("id", packageInput.Identity.Id);
            json.Add("version", packageInput.Identity.Version.ToNormalizedString());
            json.Add("verbatimVersion", packageInput.Identity.Version.ToString());

            json.Add("created", now.GetDateString());
            json.Add("lastEdited", "0001-01-01T00:00:00Z");

            json.Add("authors", CreateProperty("authors", "authors", nuspec));

            json.Add("copyright", string.Empty);
            json.Add("description", string.Empty);
            json.Add("iconUrl", string.Empty);
            json.Add("isPrerelease", packageInput.Identity.Version.IsPrerelease);
            json.Add("licenseNames", string.Empty);
            json.Add("licenseReportUrl", string.Empty);
            json.Add("listed", true);
            json.Add("packageHash", string.Empty);
            json.Add("packageHashAlgorithm", "SHA512");

            using (var stream = File.OpenRead(packageInput.PackagePath))
            {
                json.Add("packageSize", stream.Length);
            }

            json.Add("projectUrl", string.Empty);
            json.Add("published", now.GetDateString());
            json.Add("requireLicenseAcceptance", false);
            json.Add("title", packageInput.Identity.Id);
            json.Add("tags", new JArray());

            // TODO: add files
            // TODO: add sleet properties here such as username

            return JsonLDTokenComparer.Format(json);
        }

        private static JProperty CreateProperty(string catalogName, string nuspecName, XDocument nuspec)
        {
            var xmlRoot = nuspec.Root.Elements().Where(e => StringComparer.Ordinal.Equals(e.Name.LocalName, "metadata")).FirstOrDefault();
            var element = xmlRoot.Element(XName.Get(nuspecName));

            return new JProperty(catalogName, element.ToString() ?? string.Empty);
        }
    }
}
