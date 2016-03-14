using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            catalogIndexJson["commitTimeStamp"] = _context.Now.GetDateString();

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
                newPage["commitTimeStamp"] = _context.Now.GetDateString();
                newPage["count"] = 0;
                newPage["sleet:pageIndex"] = pages.Count;

                newPage = JsonLDTokenComparer.Format(newPage);

                var pageArray = (JArray)catalogIndexJson["items"];
                pageArray.Add(newPage);

                // Update pages
                pages = GetItems(catalogIndexJson);
            }

            // Create commit
            var pageCommit = JsonUtility.Create(packageDetailsFile.Path, "nuget:PackageDetails");
            pageCommit["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
            pageCommit["commitTimeStamp"] = _context.Now.GetDateString();
            pageCommit["nuget:id"] = packageInput.Identity.Id;
            pageCommit["nuget:version"] = packageInput.Identity.Version.ToNormalizedString();
            pageCommit["sleet:operation"] = "add";

            pageCommits.Add(pageCommit);

            // Write catalog page
            var pageJson = CreateCatalogPage(catalogIndexUri, currentPageUri, pageCommits);

            await currentPageFile.Write(pageJson, _context.Log, _context.Token);

            // Update index
            var pageEntry = pages.Where(e => e["@id"].ToString() == currentPageFile.Path.AbsoluteUri).Single();
            pageEntry["commitId"] = _context.CommitId.ToString().ToLowerInvariant();
            pageEntry["commitTimeStamp"] = _context.Now.GetDateString();
            pageEntry["count"] = pageCommits.Count;

            catalogIndexJson["count"] = pages.Count;

            // TODO: set these correctly
            catalogIndexJson["nuget:lastCreated"] = _context.Now.GetDateString();
            catalogIndexJson["nuget:lastDeleted"] = _context.Now.GetDateString();
            catalogIndexJson["nuget:lastEdited"] = _context.Now.GetDateString();

            catalogIndexJson = JsonLDTokenComparer.Format(catalogIndexJson);

            await catalogIndexFile.Write(catalogIndexJson, _context.Log, _context.Token);
        }

        public Task<bool> RemovePackage(PackageIdentity package)
        {
            throw new NotImplementedException();
        }

        public JObject CreateCatalogPage(Uri indexUri, Uri rootUri, List<JObject> packageDetails)
        {
            var json = JsonUtility.Create(rootUri, "CatalogPage");
            json.Add("commitId", _context.CommitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", _context.Now.GetDateString());
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

        public Uri GetCurrentPage(JObject indexJson)
        {
            var entries = GetItems(indexJson);
            var latestId = 0;

            var latest = entries.OrderByDescending(e => e["sleet:pageIndex"].ToObject<int>()).FirstOrDefault();

            if (latest != null)
            {
                latestId = latest["sleet:pageIndex"].ToObject<int>();

                if (latest["count"].ToObject<int>() < _context.SourceSettings.CatalogPageSize)
                {
                    return new Uri(latest["@id"].ToString());
                }

                latestId++;

                return _context.Source.GetPath($"/catalog/page.{latestId}.json");
            }
             
            // First page
            return _context.Source.GetPath($"/catalog/page.{latestId}.json");
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

        public async Task<bool> Exists(PackageIdentity packageIdentity)
        {
            var mostRecent = await GetLatestEntry(packageIdentity);

            return mostRecent != null && GetOperation(mostRecent) != "remove";
        }

        public async Task<JObject> GetLatestEntry(PackageIdentity package)
        {
            var pages = await GetPages();

            return pages.SelectMany(GetItems).Where(e => GetIdentity(e) == package).OrderByDescending(GetCommitTime).FirstOrDefault();
        }

        private static DateTimeOffset GetCommitTime(JObject json)
        {
            return json["commitTimeStamp"].ToObject<DateTimeOffset>();
        }

        private static string GetOperation(JObject json)
        {
            return json["sleet:operation"].ToObject<string>();
        }

        private static PackageIdentity GetIdentity(JObject json)
        {
            return new PackageIdentity(json["nuget:id"].ToString(), NuGetVersion.Parse(json["nuget:version"].ToString()));
        }

        public async Task<List<JObject>> GetPages()
        {
            var pageTasks = new List<Task<JObject>>();

            await Task.WhenAll(pageTasks);

            return pageTasks.Select(e => e.Result).ToList();
        }

        public JObject CreatePackageDetails(PackageInput packageInput)
        {
            var now = _context.Now;
            var date = now.ToString("yyyy.MM.dd.HH.mm.ss");
            var package = packageInput.Package;
            var nuspec = XDocument.Load(package.GetNuspec());
            var nuspecReader = new NuspecReader(nuspec);

            var rootUri = new Uri($"{_context.Source.Root}catalog/data/{date}/{packageInput.Identity.Id.ToLowerInvariant()}.{packageInput.Identity.Version.ToNormalizedString().ToLowerInvariant()}.json");
            packageInput.PackageDetailsUri = rootUri;

            var json = JsonUtility.Create(rootUri, new List<string>() { "PackageDetails", "catalog:Permalink" });
            json.Add("commitId", _context.CommitId.ToString().ToLowerInvariant());
            json.Add("commitTimeStamp", _context.Now.GetDateString());
            json.Add("sleet:operation", "add");

            var context = JsonUtility.GetContext("Catalog");
            json.Add("@context", context);

            json.Add("id", packageInput.Identity.Id);
            json.Add("version", packageInput.Identity.Version.ToNormalizedString());
            json.Add("verbatimVersion", packageInput.Identity.Version.ToString());

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
                json.Add("minClientVersion", minVersion.ToNormalizedString());
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

            // TODO: add files
            // TODO: add sleet properties here such as username

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
    }
}
