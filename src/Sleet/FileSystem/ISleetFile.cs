using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Logging;

namespace Sleet
{
    public interface ISleetFile
    {
        /// <summary>
        /// Actual path
        /// </summary>
        Uri RootPath { get; }

        /// <summary>
        /// Full URI used for @ids
        /// </summary>
        Uri EntityUri { get; }

        /// <summary>
        /// Overwrite the file with the given stream.
        /// </summary>
        Task Write(Stream stream, ILogger log, CancellationToken token);

        /// <summary>
        /// Save file
        /// </summary>
        Task Push(ILogger log, CancellationToken token);

        /// <summary>
        /// True if the file exists remotely
        /// </summary>
        Task<bool> Exists(ILogger log, CancellationToken token);

        ISleetFileSystem FileSystem { get; }

        Task<JObject> GetJson(ILogger log, CancellationToken token);

        Task Write(JObject json, ILogger log, CancellationToken token);

        void Delete(ILogger log, CancellationToken token);

        /// <summary>
        /// True if the file has changed.
        /// </summary>
        bool HasChanges { get; }

        Task<Stream> GetStream(ILogger log, CancellationToken token);
    }
}
