using System;
using System.Diagnostics;

namespace Sleet
{
    public static class PerfEntryWrapper
    {
        public static PerfEntryWrapper<PerfFileEntry> CreateModifyTimer(ISleetFile file, SleetContext context)
        {
            return CreateModifyTimer(file, context.PerfTracker);
        }

        public static PerfEntryWrapper<PerfFileEntry> CreateModifyTimer(ISleetFile file, IPerfTracker tracker)
        {
            return CreateFileTimer(file, tracker, PerfFileEntry.FileOperation.Modify);
        }

        public static PerfEntryWrapper<PerfFileEntry> CreateFileTimer(ISleetFile file, IPerfTracker tracker, PerfFileEntry.FileOperation operation)
        {
            return new PerfEntryWrapper<PerfFileEntry>(tracker, (time) => new PerfFileEntry(time, file.EntityUri, operation));
        }

        public static PerfEntryWrapper<PerfSummaryEntry> CreateSummaryTimer(string message, IPerfTracker tracker)
        {
            return new PerfEntryWrapper<PerfSummaryEntry>(tracker, (time) => new PerfSummaryEntry(time, message));
        }

        public static PerfEntryWrapper<PerfSummaryEntry> CreateSummaryTimer(string message, IPerfTracker tracker, TimeSpan minTimeToShow)
        {
            return new PerfEntryWrapper<PerfSummaryEntry>(tracker, (time) => new PerfSummaryEntry(time, message, minTimeToShow));
        }
    }

    /// <summary>
    /// Time an action and log the result to the perf tracker.
    /// </summary>
    public class PerfEntryWrapper<T> : IDisposable where T : PerfEntryBase
    {
        private readonly IPerfTracker _tracker;
        private readonly Func<TimeSpan, T> _getEntry;
        private readonly Stopwatch _timer = Stopwatch.StartNew();

        public PerfEntryWrapper(IPerfTracker tracker, Func<TimeSpan, T> getEntry)
        {
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            _getEntry = getEntry ?? throw new ArgumentNullException(nameof(getEntry));
        }

        public void Dispose()
        {
            // Log the event
            _timer.Stop();
            _tracker.Add(_getEntry(_timer.Elapsed));
        }
    }
}
