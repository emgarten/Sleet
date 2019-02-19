using System;
using System.Collections.Generic;
using System.Linq;

namespace Sleet
{
    /// <summary>
    /// A basic perf log message that displays the time for an event.
    /// </summary>
    public class PerfSummaryEntry : PerfEntryBase
    {
        public DateTimeOffset Created { get; }

        public override string Key { get; }

        public PerfSummaryEntry(TimeSpan elapsedTime, string summaryMessage)
            : this(elapsedTime, summaryMessage, TimeSpan.Zero)
        {
        }

        public PerfSummaryEntry(TimeSpan elapsedTime, string summaryMessage, TimeSpan minTimeToShow)
         : this(elapsedTime, summaryMessage, minTimeToShow, DateTimeOffset.UtcNow)
        {
        }

        public PerfSummaryEntry(TimeSpan elapsedTime, string summaryMessage, TimeSpan minTimeToShow, DateTimeOffset created)
            : base(elapsedTime, minTimeToShow)
        {
            Key = summaryMessage;
            Created = created;
        }

        public override string GetMessage(TimeSpan timeSpan)
        {
            return string.Format(Key, PrintUtility.GetTimeString(timeSpan));
        }

        public static PerfSummaryEntry Merge(List<PerfSummaryEntry> entries)
        {
            if (entries.Count == 1)
            {
                return entries[0];
            }

            var oldest = entries.OrderBy(e => e.Created).First();
            var totalTime = entries.Select(e => e.ElapsedTime).Aggregate(TimeSpan.Zero, (sum, e) => sum.Add(e));

            return oldest.WithElapsedTime(totalTime);
        }

        public PerfSummaryEntry WithElapsedTime(TimeSpan elapsedTime)
        {
            return new PerfSummaryEntry(elapsedTime, Key, MinTimeToShow, Created);
        }
    }
}
