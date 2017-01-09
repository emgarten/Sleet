using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;

namespace Sleet
{
    /// <summary>
    /// Search writes all packages into a single static search result file.
    /// </summary>
    public class Search : ISleetService, IRootIndex, IPackagesLookup
    {
        private readonly SleetContext _context;
        public string RootIndex { get; } = "search/query";

        public string Name { get; } = nameof(Search);

        public Search(SleetContext context)
        {
            _context = context;
        }

        public async Task AddPackageAsync(PackageInput packageInput)
        {
            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            // Read existing entries
            var data = GetData(json);

            // Remove the package id we are adding
            data.RemoveAll(e => packageInput.Identity.Id.Equals(e.GetId(), StringComparison.OrdinalIgnoreCase));

            // Rebuild the new entry
            var newEntry = await CreatePackageEntry(packageInput.Identity, add: true);
            data.Add(newEntry);

            json = CreatePage(data);

            // Write the result
            await file.Write(json, _context.Log, _context.Token);
        }

        public async Task RemovePackageAsync(PackageIdentity packageIdentity)
        {
            var packageIndex = new PackageIndex(_context);
            var versions = await packageIndex.GetPackageVersions(packageIdentity.Id);

            // Noop if the id does not exist
            if (!versions.Contains(packageIdentity.Version))
            {
                return;
            }

            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            var data = GetData(json);

            data.RemoveAll(e => packageIdentity.Id.Equals(e.GetId(), StringComparison.OrdinalIgnoreCase));

            if (versions.Count > 1)
            {
                // Remove the version if others still exist, otherwise leave the entire entry out
                var newEntry = await CreatePackageEntry(packageIdentity, add: false);
                data.Add(newEntry);
            }

            json = CreatePage(data);

            await file.Write(json, _context.Log, _context.Token);
        }

        public ISleetFile RootIndexFile
        {
            get
            {
                var file = _context.Source.Get(RootIndex);
                return file;
            }
        }

        private JObject CreatePage(List<JObject> data)
        {
            var page = JObject.Parse(TemplateUtility.LoadTemplate("Search", _context.OperationStart, _context.Source.BaseURI));

            page["totalHits"] = data.Count;
            var dataArray = new JArray();
            page["data"] = dataArray;

            foreach (var entry in data.OrderBy(e => e.GetId(), StringComparer.OrdinalIgnoreCase))
            {
                dataArray.Add(entry);
            }

            return JsonLDTokenComparer.Format(page);
        }

        /// <summary>
        /// Create a result containing all versions of the package. The passed in identity
        /// may or may not be the latest one that is shown.
        /// </summary>
        private async Task<JObject> CreatePackageEntry(PackageIdentity package, bool add)
        {
            var packageIndex = new PackageIndex(_context);
            var versions = await packageIndex.GetPackageVersions(package.Id);

            if (add)
            {
                versions.Add(package.Version);
            }
            else
            {
                versions.Remove(package.Version);
            }

            var latest = versions.Max();
            var latestIdentity = new PackageIdentity(package.Id, latest);

            var packageUri = Registrations.GetPackageUri(_context.Source.BaseURI, latestIdentity);
            var packageEntry = JsonUtility.Create(packageUri, "Package");

            var registrationUri = Registrations.GetIndexUri(_context.Source.BaseURI, package.Id);

            var catalog = new Catalog(_context);
            var catalogEntry = await catalog.GetLatestPackageDetailsAsync(latestIdentity);

            Debug.Assert(catalogEntry != null);

            packageEntry.Add("registration", registrationUri.AbsoluteUri);

            var copyProperties = new[]
            {
                "id",
                "version",
                "description",
                "summary",
                "title",
                "iconUrl",
                "licenseUrl",
                "projectUrl",
                "tags"
            };

            JsonUtility.CopyProperties(catalogEntry, packageEntry, copyProperties, skipEmpty: false);

            var copyPropertiesDelimited = new[]
            {
                "authors",
                "owners"
            };

            JsonUtility.CopyDelimitedProperties(catalogEntry, packageEntry, copyPropertiesDelimited, ',');

            JsonUtility.RequireArrayWithEmptyString(packageEntry, new[] { "tags", "authors" });

            packageEntry.Add("totalDownloads", 0);

            var versionsArray = new JArray();
            packageEntry.Add("versions", versionsArray);

            foreach (var version in versions.OrderBy(v => v))
            {
                var versionIdentity = new PackageIdentity(package.Id, version);
                var versionUri = Registrations.GetPackageUri(_context.Source.BaseURI, versionIdentity);

                var versionEntry = JsonUtility.Create(versionUri, "Package");
                versionEntry.Add("downloads", 0);
                versionEntry.Add("version", version.ToFullVersionString());

                versionsArray.Add(versionEntry);
            }

            return JsonLDTokenComparer.Format(packageEntry);
        }

        private List<JObject> GetData(JObject page)
        {
            return page.GetJObjectArray("data").ToList();
        }

        /// <summary>
        /// Find all packages listed in search, and all versions of those package ids.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesAsync()
        {
            var packages = new HashSet<PackageIdentity>();

            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            foreach (var data in GetData(json))
            {
                var id = data.GetId();

                foreach (var versionEntry in data.GetJObjectArray("versions"))
                {
                    var identity = new PackageIdentity(id, versionEntry.GetVersion());

                    packages.Add(identity);
                }
            }

            return packages;
        }

        /// <summary>
        /// Find all packages of the given id.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesByIdAsync(string packageId)
        {
            var allPackages = await GetPackagesAsync();

            return new HashSet<PackageIdentity>(allPackages.Where(e => e.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)));
        }
    }
}