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
    public class AutoComplete : ISleetService
    {
        private readonly SleetContext _context;
        public static readonly string FilePath = "autocomplete/query";

        public AutoComplete(SleetContext context)
        {
            _context = context;
        }

        public async Task AddPackage(PackageInput packageInput)
        {
            var file = _context.Source.Get(FilePath);
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

        public async Task<bool> RemovePackage(PackageIdentity packageIdentity)
        {
            var file = _context.Source.Get(FilePath);
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

                return true;
            }

            return false;
        }
    }
}
