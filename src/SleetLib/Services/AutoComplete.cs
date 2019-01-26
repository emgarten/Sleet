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

        private async Task<JObject> GetEmptyTemplate()
        {
            // Load the template from the resource file
            // Start/URI are not used in the file currently
            var template = await TemplateUtility.LoadTemplate("AutoComplete", _context.OperationStart, _context.Source.BaseURI);

            return JObject.Parse(template);
        }

        public Task ApplyOperationsAsync(SleetOperations operations)
        {
            return CreateAsync(operations.UpdatedIndex.Packages.Index.Select(e => e.Id));
        }

        public Task PreLoadAsync(SleetOperations operations)
        {
            // Noop
            return Task.FromResult(true);
        }
    }
}