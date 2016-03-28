using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    public class Catalog : ISleetService, IPackagesLookup, IRootIndex
    {
        private readonly SleetContext _context;

        public string Name { get; } = "Catalog";

        public string RootIndex { get; } = "catalog/index.json";

        public Catalog(SleetContext context)
        {
            _context = context;
        }

        public ISleetFile RootIndexFile
        {
            get
            {
                return _context.Source.Get(RootIndex);
            }
        }

        /// <summary>
        /// Add a package to the catalog.
        /// </summary>
        public async Task AddPackage(PackageInput packageInput)
        {
            // Create package details page
            var packageDetails = CreatePackageDetails(packageInput);
            var packageDetailsFile = _context.Source.Get(new Uri(packageDetails["@id"].ToString()));
            await packageDetailsFile.Write(packageDetails, _context.Log, _context.Token);

            // Add catalog page entry
            var catalogIndexUri = _context.Source.GetPath("/catalog/index.json");
            var catalogIndexFile = _context.Source.Get(catalogIndexUri);
            var catalogIndexJson = await catalogIndexFile.GetJson(_context.Log, _context.Token);

            catalogIndexJson["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
            catalogIndexJson["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();

            var pages = GetItems(catalogIndexJson);

            var currentPageUri = GetCurrentPage(catalogIndexJson);
            var currentPageFile = _context.Source.Get(currentPageUri);

            var pageCommits = new List<JObject>();

            if (await currentPageFile.Exists(_context.Log, _context.Token))
            {
                var currentPageJson = await currentPageFile.GetJson(_context.Log, _context.Token);

                pageCommits = GetItems(currentPageJson);
            }
            else
            {
                var newPage = JsonUtility.Create(currentPageFile.Path, "CatalogPage");
                newPage["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
                newPage["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();
                newPage["count"] = 0;

                newPage = JsonLDTokenComparer.Format(newPage);

                var pageArray = (JArray)catalogIndexJson["items"];
                pageArray.Add(newPage);

                // Update pages
                pages = GetItems(catalogIndexJson);
            }

            // Create commit
            var pageCommit = JsonUtility.Create(packageDetailsFile.Path, "nuget:PackageDetails");
            pageCommit["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
            pageCommit["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();
            pageCommit["nuget:id"] = packageInput.Identity.Id;
            pageCommit["nuget:version"] = packageInput.Identity.Version.ToFullVersionString();
            pageCommit["sleet:operation"] = "add";

            pageCommits.Add(pageCommit);

            // Write catalog page
            var pageJson = CreateCatalogPage(catalogIndexUri, currentPageUri, pageCommits);

            await currentPageFile.Write(pageJson, _context.Log, _context.Token);

            // Update index
            var pageEntry = pages.Where(e => e["@id"].ToString() == currentPageFile.Path.AbsoluteUri).Single();
            pageEntry["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
            pageEntry["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();
            pageEntry["count"] = pageCommits.Count;

            catalogIndexJson["count"] = pages.Count;
            catalogIndexJson["nuget:lastCreated"] = DateTimeOffset.UtcNow.GetDateString();

            catalogIndexJson = JsonLDTokenComparer.Format(catalogIndexJson);

            await catalogIndexFile.Write(catalogIndexJson, _context.Log, _context.Token);
        }

        public async Task RemovePackage(PackageIdentity package)
        {
            // Create package details page
            var packageDetails = CreateDeleteDetails(package, string.Empty);
            var packageDetailsFile = _context.Source.Get(packageDetails.GetEntityId());
            await packageDetailsFile.Write(packageDetails, _context.Log, _context.Token);

            // Add catalog page entry
            var catalogIndexUri = _context.Source.GetPath("/catalog/index.json");
            var catalogIndexFile = _context.Source.Get(catalogIndexUri);
            var catalogIndexJson = await catalogIndexFile.GetJson(_context.Log, _context.Token);

            catalogIndexJson["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
            catalogIndexJson["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();

            var pages = GetItems(catalogIndexJson);

            var currentPageUri = GetCurrentPage(catalogIndexJson);
            var currentPageFile = _context.Source.Get(currentPageUri);

            var pageCommits = new List<JObject>();

            if (await currentPageFile.Exists(_context.Log, _context.Token))
            {
                var currentPageJson = await currentPageFile.GetJson(_context.Log, _context.Token);

                pageCommits = GetItems(currentPageJson);
            }
            else
            {
                var newPage = JsonUtility.Create(currentPageFile.Path, "CatalogPage");
                newPage["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
                newPage["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();
                newPage["count"] = 0;

                newPage = JsonLDTokenComparer.Format(newPage);

                var pageArray = (JArray)catalogIndexJson["items"];
                pageArray.Add(newPage);

                // Update pages
                pages = GetItems(catalogIndexJson);
            }

            // Create commit
            var pageCommit = JsonUtility.Create(packageDetailsFile.Path, "nuget:PackageDetails");
            pageCommit["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
            pageCommit["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();
            pageCommit["nuget:id"] = package.Id;
            pageCommit["nuget:version"] = package.Version.ToFullVersionString();
            pageCommit["sleet:operation"] = "remove";

            pageCommits.Add(pageCommit);

            // Write catalog page
            var pageJson = CreateCatalogPage(catalogIndexUri, currentPageUri, pageCommits);

            await currentPageFile.Write(pageJson, _context.Log, _context.Token);

            // Update index
            var pageEntry = pages.Where(e => e["@id"].ToString() == currentPageFile.Path.AbsoluteUri).Single();
            pageEntry["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
            pageEntry["commitTimeStamp"] = DateTimeOffset.UtcNow.GetDateString();
            pageEntry["count"] = pageCommits.Count;

            catalogIndexJson["count"] = pages.Count;
            catalogIndexJson["nuget:lastDeleted"] = DateTimeOffset.UtcNow.GetDateString();

            catalogIndexJson = JsonLDTokenComparer.Format(catalogIndexJson);

            await catalogIndexFile.Write(catalogIndexJson, _context.Log, _context.Token);
        }

        /// <summary>
        /// Catalog index page.
        /// </summary>
        public JObject CreateCatalogPage(Uri indexUri, Uri rootUri, List<JObject> packageDetails)
        {
            var json = JsonUtility.Create(rootUri, "CatalogPage");
            json.Add("commitId", _context.CommitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", DateTimeOffset.UtcNow.GetDateString());
            json.Add("count", packageDetails.Count);
            json.Add("parent", indexUri.AbsoluteUri);

            var itemArray = new JArray();
            json.Add("items", itemArray);

            foreach (var entry in packageDetails
                .OrderBy(e => e["commitTimeStamp"].ToObject<DateTimeOffset>())
                .ThenBy(e => e["@id"].ToString()))
            {
                itemArray.Add(entry);
            }

            var context = JsonUtility.GetContext("CatalogPage");
            json.Add("@context", context);

            return JsonLDTokenComparer.Format(json);
        }

        /// <summary>
        /// Uri of the latest index page.
        /// </summary>
        public Uri GetCurrentPage(JObject indexJson)
        {
            var entries = GetItems(indexJson);
            var nextId = entries.Count;

            var latest = entries.OrderByDescending(GetCommitTime).FirstOrDefault();

            if (latest != null)
            {
                if (latest["count"].ToObject<int>() < _context.SourceSettings.CatalogPageSize)
                {
                    return new Uri(latest["@id"].ToString());
                }

                return _context.Source.GetPath($"/catalog/page.{nextId}.json");
            }

            // First page
            return _context.Source.GetPath($"/catalog/page.{nextId}.json");
        }

        /// <summary>
        /// Get items from a page or index page.
        /// </summary>
        public static List<JObject> GetItems(JObject json)
        {
            var result = new List<JObject>();
            var items = json["items"] as JArray;

            if (items != null)
            {
                foreach (var item in items)
                {
                    result.Add((JObject)item);
                }
            }

            return result;
        }

        /// <summary>
        /// True if the package exists in the catalog and has not been removed.
        /// </summary>
        public async Task<bool> Exists(PackageIdentity packageIdentity)
        {
            var mostRecent = await GetLatestEntry(packageIdentity);

            return mostRecent?.Operation == SleetOperation.Add;
        }

        /// <summary>
        /// Returns all pages from the catalog index.
        /// </summary>
        public async Task<List<JObject>> GetPages()
        {
            var pageTasks = new List<Task<JObject>>();

            var catalogIndexUri = _context.Source.GetPath("/catalog/index.json");
            var catalogIndexFile = _context.Source.Get(catalogIndexUri);
            var catalogIndexJson = await catalogIndexFile.GetJson(_context.Log, _context.Token);

            var items = (JArray)catalogIndexJson["items"];

            foreach (var item in items)
            {
                var itemUrl = item["@id"].ToObject<Uri>();

                var itemFile = _context.Source.Get(itemUrl);

                pageTasks.Add(itemFile.GetJson(_context.Log, _context.Token));
            }

            await Task.WhenAll(pageTasks);

            return pageTasks.Select(e => e.Result).ToList();
        }

        /// <summary>
        /// Create PackageDetails for a delete
        /// </summary>
        public JObject CreateDeleteDetails(PackageIdentity package, string reason)
        {
            var now = DateTimeOffset.UtcNow;
            var pageId = Guid.NewGuid().ToString().ToLowerInvariant();

            var rootUri = new Uri($"{_context.Source.Root}catalog/data/{pageId}.json");

            var json = JsonUtility.Create(rootUri, new List<string>() { "PackageDetails", "catalog:Permalink" });
            json.Add("commitId", _context.CommitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", now.GetDateString());
            json.Add("sleet:operation", "remove");

            var context = JsonUtility.GetContext("Catalog");
            json.Add("@context", context);

            json.Add("id", package.Id);
            json.Add("version", package.Version.ToFullVersionString());

            json.Add("created", DateTimeOffset.UtcNow.GetDateString());
            json.Add("sleet:removeReason", reason);

            json.Add("sleet:toolVersion", Constants.SleetVersion.ToFullVersionString());

            return JsonLDTokenComparer.Format(json);
        }

        /// <summary>
        /// Create a PackageDetails page that contains all the package information.
        /// </summary>
        public JObject CreatePackageDetails(PackageInput packageInput)
        {
            var now = DateTimeOffset.UtcNow;
            var package = packageInput.Package;
            var nuspec = XDocument.Load(package.GetNuspec());
            var nuspecReader = new NuspecReader(nuspec);

            var pageId = Guid.NewGuid().ToString().ToLowerInvariant();

            var rootUri = new Uri($"{_context.Source.Root}catalog/data/{pageId}.json");
            packageInput.PackageDetailsUri = rootUri;

            var json = JsonUtility.Create(rootUri, new List<string>() { "PackageDetails", "catalog:Permalink" });
            json.Add("commitId", _context.CommitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", DateTimeOffset.UtcNow.GetDateString());
            json.Add("sleet:operation", "add");

            var context = JsonUtility.GetContext("Catalog");
            json.Add("@context", context);

            json.Add("id", packageInput.Identity.Id);
            json.Add("version", packageInput.Identity.Version.ToFullVersionString());

            json.Add("created", now.GetDateString());
            json.Add("lastEdited", "0001-01-01T00:00:00Z");

            var copyProperties = new List<string>()
            {
                "authors",
                "copyright",
                "description",
                "iconUrl",
                "projectUrl",
                "licenseUrl",
                "language",
                "summary",
                "owners",
                "releaseNotes"
            };

            foreach (var propertyName in copyProperties)
            {
                json.Add(CreateProperty(propertyName, propertyName, nuspecReader));
            }

            json.Add("isPrerelease", packageInput.Identity.Version.IsPrerelease);

            // Unused?
            json.Add("licenseNames", string.Empty);
            json.Add("licenseReportUrl", string.Empty);

            // All packages are listed
            json.Add("listed", true);

            var titleValue = GetEntry(nuspecReader, "title");
            if (!string.IsNullOrEmpty(titleValue))
            {
                json.Add("title", titleValue);
            }

            using (var stream = File.OpenRead(packageInput.PackagePath))
            {
                using (var sha512 = SHA512.Create())
                {
                    var packageHash = Convert.ToBase64String(sha512.ComputeHash(stream));

                    json.Add("packageHash", packageHash);
                    json.Add("packageHashAlgorithm", "SHA512");
                }

                json.Add("packageSize", stream.Length);
            }

            json.Add("published", now.GetDateString());
            json.Add("requireLicenseAcceptance", GetEntry(nuspecReader, "requireLicenseAcceptance").Equals("true", StringComparison.OrdinalIgnoreCase));

            var minVersion = nuspecReader.GetMinClientVersion();

            if (minVersion != null)
            {
                json.Add("minClientVersion", minVersion.ToIdentityString());
            }

            // Tags
            var tagSet = new HashSet<string>(GetEntry(nuspecReader, "tags").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            tagSet.Remove(string.Empty);
            var tagArray = new JArray(tagSet);
            json.Add("tags", tagArray);

            // Framework assemblies
            var fwrGroups = nuspecReader.GetFrameworkReferenceGroups();
            var fwrArray = new JArray();
            json.Add("frameworkAssemblyGroup", fwrArray);

            foreach (var group in fwrGroups.OrderBy(e => e.TargetFramework.GetShortFolderName(), StringComparer.OrdinalIgnoreCase))
            {
                var groupTFM = group.TargetFramework.GetShortFolderName().ToLowerInvariant();
                var groupNode = JsonUtility.Create(rootUri, $"frameworkassemblygroup/{groupTFM}".ToLowerInvariant(), "FrameworkAssemblyGroup");

                // Leave the framework property out for the 'any' group
                if (!group.TargetFramework.IsAny)
                {
                    groupNode.Add("targetFramework", groupTFM);
                }

                fwrArray.Add(groupNode);

                if (group.Items.Any())
                {
                    var assemblyArray = new JArray();
                    groupNode.Add("assembly", assemblyArray);

                    foreach (var fwAssembly in group.Items.Distinct().OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
                    {
                        assemblyArray.Add(fwAssembly);
                    }
                }
            }

            // Dependencies
            var dependencyGroups = nuspecReader.GetDependencyGroups();

            var depArray = new JArray();
            json.Add("dependencyGroups", depArray);

            foreach (var group in dependencyGroups.OrderBy(e => e.TargetFramework.GetShortFolderName(), StringComparer.OrdinalIgnoreCase))
            {
                var groupTFM = group.TargetFramework.GetShortFolderName().ToLowerInvariant();
                var groupNode = JsonUtility.Create(rootUri, $"dependencygroup/{groupTFM}".ToLowerInvariant(), "PackageDependencyGroup");

                // Leave the framework property out for the 'any' group
                if (!group.TargetFramework.IsAny)
                {
                    groupNode.Add("targetFramework", groupTFM);
                }

                depArray.Add(groupNode);

                if (group.Packages.Any())
                {
                    var packageArray = new JArray();
                    groupNode.Add("dependencies", packageArray);

                    foreach (var depPackage in group.Packages.Distinct().OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        var packageNode = JsonUtility.Create(rootUri, $"dependencygroup/{groupTFM}/{depPackage.Id}".ToLowerInvariant(), "PackageDependency");
                        packageNode.Add("id", depPackage.Id);
                        packageNode.Add("range", depPackage.VersionRange.ToNormalizedString());

                        packageArray.Add(packageNode);
                    }
                }
            }

            json.Add("packageContent", packageInput.NupkgUri.AbsoluteUri);

            // add flatcontainer files
            var packageEntriesArray = new JArray();
            json.Add("packageEntries", packageEntriesArray);
            var packageEntryIndex = 0;

            foreach (var entry in packageInput.Zip.Entries.OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
            {
                var fileEntry = JsonUtility.Create(rootUri, $"packageEntry/{packageEntryIndex}", "packageEntry");
                fileEntry.Add("fullName", entry.FullName);
                fileEntry.Add("length", entry.Length);
                fileEntry.Add("lastWriteTime", entry.LastWriteTime.GetDateString());

                packageEntriesArray.Add(fileEntry);
                packageEntryIndex++;
            }

            json.Add("sleet:toolVersion", Constants.SleetVersion.ToFullVersionString());

            return JsonLDTokenComparer.Format(json);
        }

        private static JProperty CreateProperty(string catalogName, string nuspecName, NuspecReader nuspec)
        {
            var value = GetEntry(nuspec, nuspecName);

            return new JProperty(catalogName, value);
        }

        private static string GetEntry(NuspecReader reader, string property)
        {
            return reader.GetMetadata()
                .Where(pair => StringComparer.OrdinalIgnoreCase.Equals(pair.Key, property))
                .FirstOrDefault()
                .Value ?? string.Empty;
        }

        /// <summary>
        /// All packages that exist and have not been removed.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackages()
        {
            var existingPackages = await GetExistingPackagesIndex();

            return new HashSet<PackageIdentity>(existingPackages.Select(e => e.PackageIdentity));
        }

        /// <summary>
        /// All packages for the given id that exist and have not been removed.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesById(string packageId)
        {
            var allPackages = await GetPackages();

            return new HashSet<PackageIdentity>(allPackages.Where(e => e.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Latest index entry for the package. This could be an add or remove.
        /// </summary>
        public async Task<CatalogIndexEntry> GetLatestEntry(PackageIdentity package)
        {
            var entries = await GetRolledUpIndex();

            return entries.Where(e => e.PackageIdentity.Equals(package)).FirstOrDefault();
        }

        /// <summary>
        /// Returns the json of the latest package details page. If the package does
        /// not exist or has been removed this will be null.
        /// </summary>
        public async Task<JObject> GetLatestPackageDetails(PackageIdentity package)
        {
            JObject json = null;
            var latestEntry = await GetLatestEntry(package);

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
        public async Task<IReadOnlyList<CatalogIndexEntry>> GetIndexEntries()
        {
            var pages = await GetPages();

            // These cannot be ordered by commit time since add and remove may happen in the same commit.
            // Instead the page order needs to be used.
            return pages.SelectMany(GetItems).Select(GetIndexEntry).OrderByDescending(e => e.CommitTime).ToList();
        }

        /// <summary>
        /// Returns the latest operation for each package.
        /// </summary>
        public async Task<ISet<CatalogIndexEntry>> GetRolledUpIndex()
        {
            var entries = await GetIndexEntries();

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
        public async Task<ISet<CatalogIndexEntry>> GetExistingPackagesIndex()
        {
            var entries = await GetRolledUpIndex();

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
    }
}
