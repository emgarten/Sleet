using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class PerfTrackerTests
    {
        [Fact]
        public void PerfSummaryEntry_MergeCombinesElapsedTimeAndKeepsOldestCreatedTime()
        {
            var older = new PerfSummaryEntry(TimeSpan.FromMilliseconds(10), "Summary {0}", TimeSpan.Zero, DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
            var newer = new PerfSummaryEntry(TimeSpan.FromMilliseconds(15), "Summary {0}", TimeSpan.Zero, DateTimeOffset.Parse("2024-01-02T00:00:00Z"));

            var merged = PerfSummaryEntry.Merge(new System.Collections.Generic.List<PerfSummaryEntry> { newer, older });

            merged.ElapsedTime.Should().Be(TimeSpan.FromMilliseconds(25));
            merged.Created.Should().Be(older.Created);
            merged.ToString().Should().Be("Summary 25 ms");
        }

        [Fact]
        public void PerfFileEntry_MergeCombinesElapsedTimeAndKeepsFileAndOperation()
        {
            var first = new PerfFileEntry(TimeSpan.FromMilliseconds(10), new Uri("https://example/a.json"), PerfFileEntry.FileOperation.Get);
            var second = new PerfFileEntry(TimeSpan.FromMilliseconds(15), new Uri("https://example/a.json"), PerfFileEntry.FileOperation.Get);

            var merged = PerfFileEntry.Merge(new System.Collections.Generic.List<PerfFileEntry> { first, second });

            merged.ElapsedTime.Should().Be(TimeSpan.FromMilliseconds(25));
            merged.File.Should().Be(first.File);
            merged.Operation.Should().Be(PerfFileEntry.FileOperation.Get);
            merged.ToString().Should().Be("(GET) /a.json : 25 ms");
        }

        [Fact]
        public void PerfEntryBase_ComparisonEqualityAndVisibilityUseElapsedTimeAndKey()
        {
            var fast = new PerfSummaryEntry(TimeSpan.FromMilliseconds(10), "Summary {0}", TimeSpan.FromMilliseconds(20));
            var slow = new PerfSummaryEntry(TimeSpan.FromMilliseconds(30), "Summary {0}", TimeSpan.FromMilliseconds(20));
            var sameAsSlow = new PerfSummaryEntry(TimeSpan.FromMilliseconds(30), "Summary {0}", TimeSpan.Zero);

            fast.ShouldShow().Should().BeFalse();
            slow.ShouldShow().Should().BeTrue();
            (fast < slow).Should().BeTrue();
            (slow > fast).Should().BeTrue();
            slow.Equals(sameAsSlow).Should().BeTrue();
            slow.GetHashCode().Should().Be(sameAsSlow.GetHashCode());
            slow.CompareTo(null).Should().BePositive();
        }

        [Fact]
        public async Task PerfTracker_LogSummary_MergesEntriesAndSkipsEntriesBelowThreshold()
        {
            var tracker = new PerfTracker();
            var log = new TestLogger();
            tracker.Add(new PerfSummaryEntry(TimeSpan.FromMilliseconds(10), "Processed in {0}"));
            tracker.Add(new PerfSummaryEntry(TimeSpan.FromMilliseconds(15), "Processed in {0}"));
            tracker.Add(new PerfSummaryEntry(TimeSpan.FromMilliseconds(1), "Hidden {0}", TimeSpan.FromMilliseconds(2)));
            tracker.Add(new PerfFileEntry(TimeSpan.FromMilliseconds(20), new Uri("https://example/a.json"), PerfFileEntry.FileOperation.Put));
            tracker.Add(new PerfFileEntry(TimeSpan.FromMilliseconds(5), new Uri("https://example/a.json"), PerfFileEntry.FileOperation.Put));

            await tracker.LogSummary(log);

            log.GetMessages(LogLevel.Information).Should().Contain("====== Performance Summary ======");
            log.GetMessages(LogLevel.Information).Should().Contain("(PUT) /a.json : 25 ms");
            log.GetMessages(LogLevel.Information).Should().Contain("Processed in 25 ms");
            log.GetMessages(LogLevel.Information).Should().NotContain("Hidden");
        }

        [Fact]
        public void PerfEntryWrapper_DisposeAddsElapsedEntryToTracker()
        {
            var tracker = new PurePerfTracker();

            using (PerfEntryWrapper.CreateSummaryTimer("Timed {0}", tracker))
            {
            }

            tracker.Entry.Should().NotBeNull();
            tracker.Entry.Key.Should().Be("Timed {0}");
        }

        private sealed class PurePerfTracker : IPerfTracker
        {
            public PerfEntryBase Entry { get; private set; }

            public void Add(PerfEntryBase entry)
            {
                Entry = entry;
            }

            public Task LogSummary(ILogger log)
            {
                return Task.CompletedTask;
            }
        }
    }
}
