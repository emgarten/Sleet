using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public interface ISleetFileSystem
    {
        Uri BaseURI { get; }

        ISleetFile Get(Uri path);

        ISleetFile Get(string relativePath);

        LocalCache LocalCache { get; }

        ConcurrentDictionary<Uri, ISleetFile> Files { get; }

        Uri GetPath(string relativePath);

        Task<bool> Commit(ILogger log, CancellationToken token);

        /// <summary>
        /// Verify that a source is usable.
        /// </summary>
        Task<bool> Validate(ILogger log, CancellationToken token);
    }
}
