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
    public class Search : ISleetService
    {
        private readonly SleetContext _context;
        public static readonly string FilePath = "search/query";

        public Search(SleetContext context)
        {
            _context = context;
        }

        public async Task AddPackage(PackageInput packageInput)
        {
            var file = SearchFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            var data = GetData(json);

            data.RemoveAll(e => packageInput.Identity.Id.Equals(GetIdentity(e).Id, StringComparison.OrdinalIgnoreCase));

            var newEntry = await CreatePackageEntry(packageInput.Identity, add: true);
            data.Add(newEntry);

            json = CreatePage(data);

            await file.Write(json, _context.Log, _context.Token);
        }

        public async Task<bool> RemovePackage(PackageIdentity packageIdentity)
        {
            var packageIndex = new PackageIndex(_context);
            var versions = await packageIndex.GetPackagesWithId(packageIdentity.Id);

            if (!versions.Contains(packageIdentity.Version))
            {
                return false;
            }

            var file = SearchFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            var data = GetData(json);

            data.RemoveAll(e => packageIdentity.Id.Equals(GetIdentity(e).Id, StringComparison.OrdinalIgnoreCase));

            if (versions.Count > 1)
            {
                // Remove the version if others still exist, otherwise leave the entire entry out
                var newEntry = await CreatePackageEntry(packageIdentity, add: false);
                data.Add(newEntry);
            }

            json = CreatePage(data);

            await file.Write(json, _context.Log, _context.Token);

            return true;
        }

        private ISleetFile SearchFile
        {
            get
            {
                var file = _context.Source.Get(FilePath);
                return file;
            }
        }

        private JObject CreatePage(List<JObject> data)
        {
            var page = JObject.Parse(TemplateUtility.LoadTemplate("Search", _context.Now, _context.Source.Root));

            page["totalHits"] = data.Count;
            var dataArray = new JArray();
            page["data"] = dataArray;

            foreach (var entry in data.OrderBy(e => GetIdentity(e).Id, StringComparer.OrdinalIgnoreCase))
            {
                dataArray.Add(entry);
            }

            return JsonLDTokenComparer.Format(page);
        }

        private async Task<JObject> CreatePackageEntry(PackageIdentity package, bool add)
        {
            var packageIndex = new PackageIndex(_context);
            var versions = await packageIndex.GetPackagesWithId(package.Id);

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

            var packageUri = Registrations.GetPackageUri(_context.Source.Root, latestIdentity);
            var packageEntry = JsonUtility.Create(packageUri, "Package");

            var registrationUri = Registrations.GetIndexUri(_context.Source.Root, package.Id);

            var catalog = new Catalog(_context);
            var catalogEntry = await catalog.GetLatestEntry(latestIdentity);

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
                "tags",
                "authors"
            };

            JsonUtility.CopyProperties(catalogEntry, packageEntry, copyProperties, skipEmpty: false);

            JsonUtility.RequireArrayWithEmptyString(packageEntry, new[] { "tags", "authors" });

            packageEntry.Add("totalDownloads", 0);

            var versionsArray = new JArray();
            foreach (var version in versions.OrderBy(v => v))
            {
                var versionIdentity = new PackageIdentity(package.Id, version);
                var versionUri = Registrations.GetPackageUri(_context.Source.Root, versionIdentity);

                var versionEntry = JsonUtility.Create(versionUri, "Package");
                versionEntry.Add("downloads", 0);

                versionsArray.Add(versionEntry);
            }

            return packageEntry;
        }

        private List<JObject> GetData(JObject page)
        {
            var results = new List<JObject>();

            var data = page["data"] as JArray;

            if (data != null)
            {
                foreach (var dataEntry in data)
                {
                    results.Add((JObject)dataEntry);
                }
            }

            return results;
        }

        private PackageIdentity GetIdentity(JObject dataEntry)
        {
            return new PackageIdentity(dataEntry["id"].ToObject<string>(), NuGetVersion.Parse(dataEntry["version"].ToObject<string>()));
        }
    }
}
