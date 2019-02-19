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
    public class Search : ISleetService, IRootIndex, IPackagesLookup, IApplyOperations
    {
        private readonly SleetContext _context;
        public string RootIndex { get; } = "search/query";

        public string Name { get; } = nameof(Search);

        public Search(SleetContext context)
        {
            _context = context;
        }

        public async Task ApplyOperationsAsync(SleetOperations changeContext)
        {
            var file = RootIndexFile;
            using (var timer = PerfEntryWrapper.CreateModifyTimer(file, _context))
            {
                var json = await file.GetJson(_context.Log, _context.Token);

                // Read existing entries
                // Modified packages will be rebuilt, other entries will be left as-is.
                var data = GetData(json);

                foreach (var packageId in changeContext.GetChangedIds())
                {
                    // Remove the existing entry if it exists
                    if (data.ContainsKey(packageId))
                    {
                        data.Remove(packageId);
                    }

                    var packages = await changeContext.UpdatedIndex.Packages.GetPackagesByIdAsync(packageId);
                    var versions = new SortedSet<NuGetVersion>(packages.Select(e => e.Version));

                    // If no versions exist then there is no extra work needed.
                    if (versions.Count > 0)
                    {
                        // Rebuild the new entry
                        var newEntry = await CreatePackageEntry(packageId, versions);
                        data.Add(packageId, newEntry);
                    }
                }

                json = await CreatePage(data);

                // Write the result
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

        private async Task<JObject> CreatePage(Dictionary<string, JObject> data)
        {
            var page = JObject.Parse(await TemplateUtility.LoadTemplate("Search", _context.OperationStart, _context.Source.BaseURI));

            page["totalHits"] = data.Count;
            page["data"] = new JArray(data.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase).Select(e => e.Value));

            return JsonLDTokenComparer.Format(page, recurse: false);
        }

        /// <summary>
        /// Create a result containing all versions of the package. The passed in identity
        /// may or may not be the latest one that is shown.
        /// </summary>
        private async Task<JObject> CreatePackageEntry(string packageId, SortedSet<NuGetVersion> versions)
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

            foreach (var version in versions)
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

        private Dictionary<string, JObject> GetData(JObject page)
        {
            var data = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in page.GetJObjectArray("data"))
            {
                var id = entry.GetId();

                if (!data.ContainsKey(id))
                {
                    data.Add(id, entry);
                }
            }

            return data;
        }

        /// <summary>
        /// Find all packages listed in search, and all versions of those package ids.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesAsync()
        {
            var packages = new HashSet<PackageIdentity>();

            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            foreach (var pair in GetData(json))
            {
                var id = pair.Key;
                var data = pair.Value;

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

        public Task PreLoadAsync(SleetOperations operations)
        {
            return RootIndexFile.FetchAsync(_context.Log, _context.Token);
        }
    }
}