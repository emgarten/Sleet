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

        public ISleetFile RootIndexFile => _context.Source.Get(RootIndex);

        public Task AddPackageAsync(PackageInput packageInput)
        {
            return AddPackagesAsync(new[] { packageInput });
        }

        public Task RemovePackageAsync(PackageIdentity packageIdentity)
        {
            return RemovePackagesAsync(new[] { packageIdentity });
        }

        /// <summary>
        /// Returns all known ids.
        /// </summary>
        public async Task<ISet<string>> GetPackageIds()
        {
            var ids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var json = await RootIndexFile.GetJsonOrNull(_context.Log, _context.Token);

            if (json != null)
            {
                var data = json["data"] as JArray;
                ids.UnionWith(data.Select(e => e.ToObject<string>()).Where(s => !string.IsNullOrEmpty(s)));
            }

            return ids;
        }

        public Task FetchAsync()
        {
            return RootIndexFile.FetchAsync(_context.Log, _context.Token);
        }

        public async Task AddPackagesAsync(IEnumerable<PackageInput> packageInputs)
        {
            var ids = await GetPackageIds();
            var prevCount = ids.Count;
            ids.UnionWith(packageInputs.Select(e => e.Identity.Id));

            // Only update the file if needed.
            if (prevCount != ids.Count)
            {
                var file = RootIndexFile;
                var json = await file.GetJson(_context.Log, _context.Token);
                var data = json["data"] as JArray;
                data.Clear();

                foreach (var id in ids.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    data.Add(id);
                }

                json["totalHits"] = ids.Count;
                await file.Write(json, _context.Log, _context.Token);
            }
        }

        public async Task RemovePackagesAsync(IEnumerable<PackageIdentity> packages)
        {
            var packageIndex = new PackageIndex(_context);
            var allPackages = await packageIndex.GetPackagesAsync();
            var after = new HashSet<PackageIdentity>(allPackages.Except(packages));
            var allPackagesIds = new HashSet<string>(allPackages.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);
            var afterIds = new HashSet<string>(after.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);

            // Only update if needed
            if (allPackagesIds.Count != afterIds.Count)
            {
                var file = RootIndexFile;
                var json = await file.GetJson(_context.Log, _context.Token);
                var data = json["data"] as JArray;
                data.Clear();

                foreach (var id in afterIds.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    data.Add(id);
                }

                json["totalHits"] = after.Count;
                await file.Write(json, _context.Log, _context.Token);
            }
        }
    }
}