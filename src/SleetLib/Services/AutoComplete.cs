using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;

namespace Sleet
{
    /// <summary>
    /// Provides a list of package ids for powershell auto complete.
    /// </summary>
    public class AutoComplete : ISleetService, IRootIndex
    {
        private readonly SleetContext _context;

        public string Name { get; } = nameof(AutoComplete);

        public string RootIndex { get; } = "autocomplete/query";

        public AutoComplete(SleetContext context)
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

        public async Task AddPackageAsync(PackageInput packageInput)
        {
            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            var data = json["data"] as JArray;

            var ids = await GetPackageIds();

            ids.Add(packageInput.Identity.Id);

            data.Clear();

            foreach (var id in ids.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                data.Add(id);
            }

            json["totalHits"] = ids.Count;

            json = JsonLDTokenComparer.Format(json);

            await file.Write(json, _context.Log, _context.Token);
        }

        public async Task RemovePackageAsync(PackageIdentity packageIdentity)
        {
            var packageIndex = new PackageIndex(_context);
            var allPackagesForId = await packageIndex.GetPackagesByIdAsync(packageIdentity.Id);
            allPackagesForId.Remove(packageIdentity);

            // Only remove the package if all versions have been removed
            if (allPackagesForId.Count == 0)
            {
                var file = RootIndexFile;
                var json = await file.GetJson(_context.Log, _context.Token);

                var data = json["data"] as JArray;

                var ids = await GetPackageIds();

                if (ids.Remove(packageIdentity.Id))
                {
                    data.Clear();

                    foreach (var id in ids.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                    {
                        data.Add(id);
                    }

                    json["totalHits"] = ids.Count;

                    json = JsonLDTokenComparer.Format(json);

                    await file.Write(json, _context.Log, _context.Token);
                }
            }
        }

        /// <summary>
        /// Returns all known ids.
        /// </summary>
        public async Task<ISet<string>> GetPackageIds()
        {
            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            var data = json["data"] as JArray;
            var ids = new HashSet<string>(
                data.Select(e => e.ToObject<string>()),
                StringComparer.OrdinalIgnoreCase);

            return ids;
        }
    }
}