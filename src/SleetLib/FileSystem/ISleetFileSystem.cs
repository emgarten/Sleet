using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        /// <summary>
        /// Read all files from the feed.
        /// </summary>
        /// <remarks>This does not include the .lock file.</remarks>
        Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token);

        LocalCache LocalCache { get; }

        ConcurrentDictionary<Uri, ISleetFile> Files { get; }

        Uri GetPath(string relativePath);

        Task<bool> Commit(ILogger log, CancellationToken token);

        /// <summary>
        /// Verify that a source is usable.
        /// </summary>
        Task<bool> Validate(ILogger log, CancellationToken token);

        /// <summary>
        /// Create a file system lock.
        /// </summary>
        ISleetFileSystemLock CreateLock(ILogger log);

        /// <summary>
        /// Delete all files.
        /// </summary>
        Task<bool> Destroy(ILogger log, CancellationToken token);
    }
}