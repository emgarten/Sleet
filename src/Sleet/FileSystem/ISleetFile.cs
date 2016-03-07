using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
        /// Retrieve the local copy which can be used for reading.
        /// </summary>
        Task<FileStream> GetStream(ILogger log, CancellationToken token);

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
    }
}
