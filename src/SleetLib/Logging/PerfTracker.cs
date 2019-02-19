using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public class PerfTracker : IPerfTracker
    {
        private ConcurrentQueue<PerfEntryBase> _perfEntries = new ConcurrentQueue<PerfEntryBase>();

        public void Add(PerfEntryBase entry)
        {
            _perfEntries.Enqueue(entry);
        }

        public async Task LogSummary(ILogger log)
        {
            await log.LogAsync(LogLevel.Information, Environment.NewLine + "====== Performance Summary ======");

            var filesLookup = GetDictionary(_perfEntries.Select(e => e as PerfFileEntry).Where(e => e != null));
            var files = new List<PerfFileEntry>(filesLookup.Count);
            foreach (var set in filesLookup.Values)
            {
                files.Add(PerfFileEntry.Merge(set));
            }

            var summariesLookup = GetDictionary(_perfEntries.Select(e => e as PerfSummaryEntry).Where(e => e != null));
            var summaries = new List<PerfSummaryEntry>(summariesLookup.Count);
            foreach (var set in summariesLookup.Values)
            {
                summaries.Add(PerfSummaryEntry.Merge(set));
            }

            // Log top files
            var topFiles = files.Where(e => e.ShouldShow()).OrderByDescending(e => e.ElapsedTime).Take(5).ToList();
            if (topFiles.Count > 0)
            {
                foreach (var entry in topFiles)
                {
                    await log.LogAsync(LogLevel.Information, "  " + entry.ToString());
                }

                await log.LogAsync(LogLevel.Information, string.Empty);
            }

            // Log summaries
            foreach (var entry in summaries.Where(e => e.ShouldShow()))
            {
                await log.LogAsync(LogLevel.Information, "  " + entry.ToString());
            }
        }

        private static Dictionary<string, List<T>> GetDictionary<T>(IEnumerable<T> entries) where T : PerfEntryBase
        {
            var dict = new Dictionary<string, List<T>>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                var key = entry.Key;
                List<T> list = null;
                if (!dict.TryGetValue(key, out list))
                {
                    list = new List<T>() { entry };
                    dict.Add(key, list);
                }
                else
                {
                    list.Add(entry);
                }
            }

            return dict;
        }
    }
}
