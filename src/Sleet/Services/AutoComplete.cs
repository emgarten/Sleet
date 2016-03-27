using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Logging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class AutoComplete : ISleetService, IRootIndex
    {
        private readonly SleetContext _context;

        public string Name { get; } = nameof(AutoComplete);

        public string RootIndex { get; } = "autocomplete/query";

        public ISleetFile RootIndexFile
        {
            get
            {
                return _context.Source.Get(RootIndex);
            }
        }

        public AutoComplete(SleetContext context)
        {
            _context = context;
        }

        public async Task AddPackage(PackageInput packageInput)
        {
            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            var data = json["data"] as JArray;
            var ids = new HashSet<string>(
                data.Select(e => e.ToObject<string>()),
                StringComparer.OrdinalIgnoreCase);

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

        public async Task RemovePackage(PackageIdentity packageIdentity)
        {
            var file = RootIndexFile;
            var json = await file.GetJson(_context.Log, _context.Token);

            var data = json["data"] as JArray;
            var ids = new HashSet<string>(
                data.Select(e => e.ToObject<string>()),
                StringComparer.OrdinalIgnoreCase);

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
