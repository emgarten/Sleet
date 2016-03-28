using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;

namespace Sleet
{
    public class SleetContext
    {
        public ISleetFileSystem Source { get; set; }

        public SourceSettings SourceSettings { get; set; }

        public LocalSettings LocalSettings { get; set; }

        public ILogger Log { get; set; }

        public CancellationToken Token { get; set; }

        public Guid CommitId { get; set; } = Guid.NewGuid();

        public DateTimeOffset OperationStart { get; set; } = DateTimeOffset.UtcNow;
    }
}
