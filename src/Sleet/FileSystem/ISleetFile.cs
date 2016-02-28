using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;

namespace Sleet
{
    public interface ISleetFile
    {
        /// <summary>
        /// Full URI
        /// </summary>
        Uri Path { get; }

        /// <summary>
        /// Retrieve the local copy which can be used for reading and writing.
        /// </summary>
        Task<FileInfo> GetLocal(ILogger log, CancellationToken token);

        /// <summary>
        /// Download file
        /// </summary>
        Task Get(ILogger log, CancellationToken token);

        /// <summary>
        /// Save file
        /// </summary>
        Task Push(ILogger log, CancellationToken token);

        Task<bool> Exists(ILogger log, CancellationToken token);

        ISleetFileSystem FileSystem { get; }
    }
}
