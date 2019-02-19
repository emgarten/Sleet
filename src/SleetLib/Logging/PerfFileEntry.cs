using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sleet
{
    /// <summary>
    /// Perf stats for a specific URI
    /// </summary>
    public class PerfFileEntry : PerfEntryBase
    {
        private static readonly TimeSpan DefaultMinTime = TimeSpan.Zero;

        public Uri File { get; }

        public FileOperation Operation { get; }

        public override string Key => $"{File.AbsoluteUri}-{Operation}";

        public PerfFileEntry(TimeSpan elapsedTime, Uri file, FileOperation operation)
            : base(elapsedTime, DefaultMinTime)
        {
            File = file;
            Operation = operation;
        }

        public PerfFileEntry(TimeSpan elapsedTime, Uri file, FileOperation operation, TimeSpan minTimeToShow)
            : base(elapsedTime, minTimeToShow)
        {
            File = file;
            Operation = operation;
        }

        public override string GetMessage(TimeSpan timeSpan)
        {
            return $"({Operation.ToString().ToUpperInvariant()}) {File.PathAndQuery} : {PrintUtility.GetTimeString(timeSpan)}";
        }

        public static PerfFileEntry Merge(List<PerfFileEntry> entries)
        {
            if (entries.Count == 1)
            {
                return entries[0];
            }

            var oldest = entries[0];
            var totalTime = entries.Select(e => e.ElapsedTime).Aggregate(TimeSpan.Zero, (sum, e) => sum.Add(e));

            return oldest.WithElapsedTime(totalTime);
        }

        public PerfFileEntry WithElapsedTime(TimeSpan elapsedTime)
        {
            return new PerfFileEntry(elapsedTime, File, Operation, MinTimeToShow);
        }

        public enum FileOperation
        {
            // Download time
            Get = 0,

            // Local processing time
            Modify = 1,

            // Upload time
            Put = 2,

            // Write to disk
            LocalWrite = 3,
        }
    }
}
