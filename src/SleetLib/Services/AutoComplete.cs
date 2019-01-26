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
        // Limit results
        private const int MaxResults = 1024;
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
            // Nothing to do
            return Task.FromResult(true);
        }

        /// <summary>
        /// Create the file directly without loading the previous file.
        /// </summary>
        public async Task CreateAsync(IEnumerable<string> packageIds)
        {
            var ids = new SortedSet<string>(packageIds, StringComparer.OrdinalIgnoreCase);

            // Create a new file using the full set of package ids.
            // There is no need to read the existing file.
            var json = await GetEmptyTemplate();
            var data = new JArray(ids.Take(MaxResults));
            json["data"] = data;
            json["totalHits"] = data.Count;

            var formatted = JsonLDTokenComparer.Format(json, recurse: false);
            await RootIndexFile.Write(formatted, _context.Log, _context.Token);
        }

        // TODO: Remove this
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

        // TODO: Remove this
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

        private async Task<JObject> GetEmptyTemplate()
        {
            // Load the template from the resource file
            // Start/URI are not used in the file currently
            var template = await TemplateUtility.LoadTemplate("AutoComplete", _context.OperationStart, _context.Source.BaseURI);

            return JObject.Parse(template);
        }
    }
}