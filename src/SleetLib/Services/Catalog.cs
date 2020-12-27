using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    public class Catalog : ISleetService, IPackagesLookup, IRootIndex, IAddRemovePackages
    {
        private readonly SleetContext _context;

        public string Name { get; } = "Catalog";

        /// <summary>
        /// Example: catalog/index.json
        /// </summary>
        public string RootIndex
        {
            get
            {
                var rootURI = UriUtility.GetPath(CatalogBaseURI, "index.json");

                return UriUtility.GetRelativePath(_context.Source.BaseURI, rootURI);
            }
        }

        /// <summary>
        /// Catalog index.json file
        /// </summary>
        public ISleetFile RootIndexFile => _context.Source.Get(RootIndex);

        /// <summary>
        /// Example: http://tempuri.org/catalog/
        /// </summary>
        public Uri CatalogBaseURI { get; }

        public Catalog(SleetContext context)
            : this(context, UriUtility.GetPath(context.Source.BaseURI, "catalog/"))
        {
        }

        public Catalog(SleetContext context, Uri catalogBaseURI)
        {
            _context = context;
            CatalogBaseURI = catalogBaseURI;
        }

        /// <summary>
        /// Add a package to the catalog.
        /// </summary>
        public Task AddPackageAsync(PackageInput packageInput)
        {
            return AddPackagesAsync(new[] { packageInput });
        }

        public Task RemovePackageAsync(PackageIdentity package)
        {
            return RemovePackagesAsync(new[] { package });
        }

        public async Task AddPackagesAsync(IEnumerable<PackageInput> packageInputs)
        {
            // Write catalog pages for each package
            var tasks = packageInputs.Select(e => new Func<Task<JObject>>(() => AddPackageToCatalogAndGetCommit(e)));
            var pageCommits = await TaskUtils.RunAsync(tasks, useTaskRun: true, token: CancellationToken.None);

            // Add pages to the index as commits
            await AddCatalogCommits(pageCommits, "nuget:lastCreated");
        }

        public async Task RemovePackagesAsync(IEnumerable<PackageIdentity> packages)
        {
            // Write catalog remove pages for each package
            var tasks = packages.Select(e => new Func<Task<JObject>>(() => GetRemoveCommit(e)));
            var pageCommits = await TaskUtils.RunAsync(tasks);

            // Add pages to the index as commits
            await AddCatalogCommits(pageCommits, "nuget:lastDeleted");
        }

        /// <summary>
        /// Adds a catalog page and returns the commit.
        /// </summary>
        private async Task<JObject> AddPackageToCatalogAndGetCommit(PackageInput packageInput)
        {
            // Create package details page
            var nupkgUri = packageInput.GetNupkgUri(_context);
            var iconUri = packageInput.GetIconUri(_context);
            var packageDetails = await CatalogUtility.CreatePackageDetailsAsync(packageInput, CatalogBaseURI, nupkgUri, iconUri, _context.CommitId, writeFileList: true);
            var packageDetailsUri = JsonUtility.GetIdUri(packageDetails);

            // Add output to the package input for other services to use.
            packageInput.PackageDetails = packageDetails;

            var packageDetailsFile = _context.Source.Get(packageDetailsUri);
            await packageDetailsFile.Write(packageDetails, _context.Log, _context.Token);

            // Create commit
            return CatalogUtility.CreatePageCommit(
                packageInput.Identity,
                packageDetailsUri,
                _context.CommitId,
                SleetOperation.Add,
                "nuget:PackageDetails");
        }

        /// <summary>
        /// Add a remove entry and return the page commit.
        /// </summary>
        private async Task<JObject> GetRemoveCommit(PackageIdentity package)
        {
            // Create package details page for the delete
            var packageDetails = await CatalogUtility.CreateDeleteDetailsAsync(package, string.Empty, CatalogBaseURI, _context.CommitId);
            var packageDetailsFile = _context.Source.Get(packageDetails.GetEntityId());

            await packageDetailsFile.Write(packageDetails, _context.Log, _context.Token);

            // Create commit
            return CatalogUtility.CreatePageCommit(
                package,
                packageDetailsFile.EntityUri,
                _context.CommitId,
                SleetOperation.Remove,
                "nuget:PackageDelete");
        }

        /// <summary>
        /// Uri of the latest index page.
        /// </summary>
        public Uri GetCurrentPage(JObject indexJson)
        {
            var entries = JsonUtility.GetItems(indexJson);
            var nextId = entries.Count;

            var latest = entries.OrderByDescending(GetCommitTime).FirstOrDefault();

            if (latest != null)
            {
                if (latest["count"].ToObject<int>() < _context.SourceSettings.CatalogPageSize)
                {
                    return JsonUtility.GetIdUri(latest);
                }
            }

            // next page
            return UriUtility.GetPath(CatalogBaseURI, $"page.{nextId}.json");
        }

        /// <summary>
        /// True if the package exists in the catalog and has not been removed.
        /// </summary>
        public async Task<bool> ExistsAsync(PackageIdentity packageIdentity)
        {
            var mostRecent = await GetLatestEntryAsync(packageIdentity);

            return mostRecent?.Operation == SleetOperation.Add;
        }

        /// <summary>
        /// Returns all pages from the catalog index.
        /// </summary>
        public async Task<List<JObject>> GetPagesAsync()
        {
            var pageTasks = new List<Task<JObject>>();

            var catalogIndexJson = await RootIndexFile.GetJsonOrNull(_context.Log, _context.Token);

            if (catalogIndexJson != null)
            {
                var items = (JArray)catalogIndexJson["items"];

                foreach (var item in items)
                {
                    var itemUrl = item["@id"].ToObject<Uri>();

                    var itemFile = _context.Source.Get(itemUrl);

                    pageTasks.Add(itemFile.GetJson(_context.Log, _context.Token));
                }

                await Task.WhenAll(pageTasks);
            }

            return pageTasks.Select(e => e.Result).ToList();
        }

        /// <summary>
        /// All packages that exist and have not been removed.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesAsync()
        {
            var existingPackages = await GetExistingPackagesIndexAsync();

            return new HashSet<PackageIdentity>(existingPackages.Select(e => e.PackageIdentity));
        }

        /// <summary>
        /// All packages for the given id that exist and have not been removed.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesByIdAsync(string packageId)
        {
            var allPackages = await GetPackagesAsync();

            return new HashSet<PackageIdentity>(allPackages.Where(e => e.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Latest index entry for the package. This could be an add or remove.
        /// </summary>
        public async Task<CatalogIndexEntry> GetLatestEntryAsync(PackageIdentity package)
        {
            var entries = await GetRolledUpIndexAsync();

            return entries.Where(e => e.PackageIdentity.Equals(package)).FirstOrDefault();
        }

        /// <summary>
        /// Returns the json of the latest package details page. If the package does
        /// not exist or has been removed this will be null.
        /// </summary>
        public async Task<JObject> GetLatestPackageDetailsAsync(PackageIdentity package)
        {
            JObject json = null;
            var latestEntry = await GetLatestEntryAsync(package);

            if (latestEntry != null && latestEntry.Operation == SleetOperation.Add)
            {
                var file = _context.Source.Get(latestEntry.PackageDetailsUrl);
                json = await file.GetJson(_context.Log, _context.Token);
            }

            return json;
        }

        /// <summary>
        /// Returns all index entries from newest to oldest.
        /// </summary>
        public async Task<IReadOnlyList<CatalogIndexEntry>> GetIndexEntriesAsync()
        {
            var pages = await GetPagesAsync();

            // These cannot be ordered by commit time since add and remove may happen in the same commit.
            // Instead the page order needs to be used.
            return pages.SelectMany(JsonUtility.GetItems).Select(GetIndexEntry).OrderByDescending(e => e.CommitTime).ToList();
        }

        /// <summary>
        /// Returns the latest operation for each package.
        /// </summary>
        public async Task<ISet<CatalogIndexEntry>> GetRolledUpIndexAsync()
        {
            var entries = await GetIndexEntriesAsync();

            var latest = new HashSet<CatalogIndexEntry>();

            // Add entries in order from newest to oldest, older entries will be ignored
            foreach (var entry in entries)
            {
                latest.Add(entry);
            }

            return latest;
        }

        /// <summary>
        /// Returns the latest operation for each package that has not been removed.
        /// </summary>
        public async Task<ISet<CatalogIndexEntry>> GetExistingPackagesIndexAsync()
        {
            var entries = await GetRolledUpIndexAsync();

            var latest = new HashSet<CatalogIndexEntry>();

            // Filtered the rolled up index on 'add' operations, 'remove' operations should be skipped
            foreach (var entry in entries.Where(e => e.Operation == SleetOperation.Add))
            {
                latest.Add(entry);
            }

            return latest;
        }

        private static DateTimeOffset GetCommitTime(JObject json)
        {
            return json["commitTimeStamp"].ToObject<DateTimeOffset>();
        }

        private static CatalogIndexEntry GetIndexEntry(JObject json)
        {
            var identity = GetIdentity(json);

            return new CatalogIndexEntry(
                id: identity.Id,
                version: identity.Version,
                commitTime: GetCommitTime(json),
                operation: GetOperation(json),
                packageDetailsUrl: json["@id"].ToObject<Uri>());
        }

        private static SleetOperation GetOperation(JObject json)
        {
            var value = json["sleet:operation"].ToObject<string>();

            switch (value.ToLowerInvariant())
            {
                case "add":
                    return SleetOperation.Add;

                case "remove":
                    return SleetOperation.Remove;
            }

            throw new InvalidDataException($"sleet:operation: {value}");
        }

        private static PackageIdentity GetIdentity(JObject json)
        {
            return new PackageIdentity(json["nuget:id"].ToString(), NuGetVersion.Parse(json["nuget:version"].ToString()));
        }

        /// <summary>
        /// Add an entry to the catalog.
        /// </summary>
        private async Task AddCatalogCommits(IEnumerable<JObject> newPageCommits, string lastUpdatedPropertyName)
        {
            // Add catalog page entry
            var catalogIndexJson = await RootIndexFile.GetJson(_context.Log, _context.Token);

            catalogIndexJson["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
            catalogIndexJson["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();

            var pages = JsonUtility.GetItems(catalogIndexJson);

            var currentPageUri = GetCurrentPage(catalogIndexJson);
            var currentPageFile = _context.Source.Get(currentPageUri);

            var pageCommits = new List<JObject>();

            var currentPageJson = await currentPageFile.GetJsonOrNull(_context.Log, _context.Token);

            if (currentPageJson != null)
            {
                pageCommits = JsonUtility.GetItems(currentPageJson);
            }
            else
            {
                var newPageEntry = CatalogUtility.CreateCatalogIndexPageEntry(currentPageUri, _context.CommitId);

                var pageArray = (JArray)catalogIndexJson["items"];
                pageArray.Add(newPageEntry);

                // Update pages
                pages = JsonUtility.GetItems(catalogIndexJson);
            }

            // Add all commits, this might go over the max catalog page size.
            // This could be improved to create new pages once over the limit if needed.
            pageCommits.AddRange(newPageCommits);

            // Write catalog page
            var pageJson = await CatalogUtility.CreateCatalogPageAsync(RootIndexFile.EntityUri, currentPageUri, pageCommits, _context.CommitId);
            await currentPageFile.Write(pageJson, _context.Log, _context.Token);

            // Update index
            CatalogUtility.UpdatePageIndex(catalogIndexJson, pageJson, _context.CommitId);

            catalogIndexJson[lastUpdatedPropertyName] = DateTimeOffset.UtcNow.GetDateString();

            catalogIndexJson = JsonLDTokenComparer.Format(catalogIndexJson);

            await RootIndexFile.Write(catalogIndexJson, _context.Log, _context.Token);
        }

        public Task ApplyOperationsAsync(SleetOperations operations)
        {
            return OperationsUtility.ApplyAddRemoveAsync(this, operations);
        }

        public Task PreLoadAsync(SleetOperations operations)
        {
            return RootIndexFile.FetchAsync(_context.Log, _context.Token);
        }
    }
}