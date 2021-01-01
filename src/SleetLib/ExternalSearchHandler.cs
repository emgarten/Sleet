using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Sleet
{
    /// <summary>
    /// Updates search URI in index.json based on externalsearch
    /// </summary>
    public class ExternalSearchHandler : IFeedSettingHandler
    {
        private readonly SleetContext _context;
        private readonly Search _search;

        public string Name => "externalsearch";

        public ExternalSearchHandler(SleetContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _search = new Search(context);
        }

        public Task Set(string value)
        {
            // Apply user set search uri
            return SetSearchUri(value);
        }

        public Task UnSet()
        {
            // Revert to default uri
            var feedSearchUri = _search.RootIndexFile.EntityUri.AbsoluteUri;
            return SetSearchUri(feedSearchUri);
        }

        private async Task SetSearchUri(string value)
        {
            var indexFile = _context.Source.Get("index.json");
            var json = await indexFile.GetJson(_context.Log, _context.Token);
            var searchEntry = GetSearchEntry(json);
            searchEntry["@id"] = value;
            await indexFile.Write(json, _context.Log, _context.Token);
        }

        private JObject GetSearchEntry(JObject serviceIndex)
        {
            var resources = (JArray)serviceIndex["resources"];
            return (JObject)resources.First(e => e["@type"].ToObject<string>().StartsWith("SearchQueryService/"));
        }
    }
}
