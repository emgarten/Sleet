using System;
using System.Threading;
using NuGet.Common;

namespace Sleet
{
    public class SleetContext
    {
        public ISleetFileSystem Source { get; set; } = null!;

        public FeedSettings SourceSettings { get; set; } = null!;

        public LocalSettings LocalSettings { get; set; } = null!;

        public ILogger Log { get; set; } = null!;

        public CancellationToken Token { get; set; }

        public Guid CommitId { get; set; } = Guid.NewGuid();

        public DateTimeOffset OperationStart { get; set; } = DateTimeOffset.UtcNow;

        public IPerfTracker PerfTracker { get; set; } = NullPerfTracker.Instance;
    }
}
