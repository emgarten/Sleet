using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

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

        public Task AddPackageAsync(PackageInput packageInput)
        {
            return AddPackagesAsync(new[] { packageInput }); 
        }

        public Task RemovePackageAsync(PackageIdentity packageIdentity)
        {
            return RemovePackagesAsync(new[] { packageIdentity });
        }

        public async Task AddPackagesAsync(IEnumerable<PackageInput> packageInputs)
        {
            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            // Read existing entries
            var data = GetData(json);

            var packageIndex = new PackageIndex(_context);

            var byId = SleetUtility.GetPackageSetsById(packageInputs, e => e.Identity.Id);

            foreach (var pair in byId)
            {
                var packageId = pair.Key;
                var versions = await packageIndex.GetPackageVersions(packageId);
                versions.UnionWith(pair.Value.Select(e => e.Identity.Version));

                // Remove the package id we are adding
                data.RemoveAll(e => packageId.Equals(e.GetId(), StringComparison.OrdinalIgnoreCase));

                // Rebuild the new entry
                var newEntry = await CreatePackageEntry(packageId, versions);
                data.Add(newEntry);
            }

            json = CreatePage(data);

            // Write the result
            await file.Write(json, _context.Log, _context.Token);
        }

        public async Task RemovePackagesAsync(IEnumerable<PackageIdentity> packages)
        {
            var byId = SleetUtility.GetPackageSetsById(packages, e => e.Id);
            var packageIndex = new PackageIndex(_context);
            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);
            var data = GetData(json);
            var modified = false;

            foreach (var pair in byId)
            {
                var packageId = pair.Key;
                var versions = await packageIndex.GetPackageVersions(packageId);
                var toRemove = new HashSet<NuGetVersion>(pair.Value.Select(e => e.Version));
                var afterRemove = new HashSet<NuGetVersion>(versions.Except(toRemove));

                // Noop if the id does not exist
                if (afterRemove.Count != versions.Count)
                {
                    modified = true;
                    data.RemoveAll(e => packageId.Equals(e.GetId(), StringComparison.OrdinalIgnoreCase));

                    if (afterRemove.Count > 0)
                    {
                        // Remove the version if others still exist, otherwise leave the entire entry out
                        var newEntry = await CreatePackageEntry(packageId, afterRemove);
                        data.Add(newEntry);
                    }
                }
            }

            if (modified)
            {
                json = CreatePage(data);
                await file.Write(json, _context.Log, _context.Token);
            }
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
        private async Task<JObject> CreatePackageEntry(string packageId, ISet<NuGetVersion> versions)
        {
            var latest = versions.Max();
            var latestIdentity = new PackageIdentity(packageId, latest);

            var packageUri = Registrations.GetPackageUri(_context.Source.BaseURI, latestIdentity);
            var packageEntry = JsonUtility.Create(packageUri, "Package");

            var registrationUri = Registrations.GetIndexUri(_context.Source.BaseURI, packageId);

            // Read the catalog entry from the package blob. The catalog may not be enabled.
            var registrations = new Registrations(_context);
            var catalogEntry = await registrations.GetCatalogEntryFromPackageBlob(latestIdentity);

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
                var versionIdentity = new PackageIdentity(packageId, version);
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

        public Task FetchAsync()
        {
            return RootIndexFile.FetchAsync(_context.Log, _context.Token);
        }
    }
}