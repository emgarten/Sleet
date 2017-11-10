using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public abstract class FileSystemBase : ISleetFileSystem
    {
        private const int MaxThreads = 4;

        /// <summary>
        /// URI written to files.
        /// </summary>
        public Uri BaseURI { get; private set; }

        /// <summary>
        /// Actual URI
        /// </summary>
        public Uri Root { get; private set; }

        public LocalCache LocalCache { get; private set; }

        public ConcurrentDictionary<Uri, ISleetFile> Files { get; private set; } = new ConcurrentDictionary<Uri, ISleetFile>();

        public string FeedSubPath { get; private set; }

        protected FileSystemBase(LocalCache cache, Uri root)
            : this(cache, root, root)
        {
        }

        protected FileSystemBase(LocalCache cache, Uri root, Uri baseUri, string feedSubPath = null)
        {
            BaseURI = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
            LocalCache = cache ?? throw new ArgumentNullException(nameof(cache));
            Root = root ?? throw new ArgumentNullException(nameof(root));
            FeedSubPath = feedSubPath;
        }

        public abstract ISleetFileSystemLock CreateLock(ILogger log);

        public abstract ISleetFile Get(Uri path);

        public abstract Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token);

        public abstract Task<bool> Validate(ILogger log, CancellationToken token);

        public async Task<bool> Commit(ILogger log, CancellationToken token)
        {
            // Push in parallel
            var tasks = new List<Task>();

            foreach (var file in Files.Values)
            {
                if (tasks.Count >= MaxThreads)
                {
                    await CompleteTask(tasks);
                }

                tasks.Add(file.Push(log, token));
            }

            while (tasks.Count > 0)
            {
                await CompleteTask(tasks);
            }

            return true;
        }

        public virtual async Task<bool> Destroy(ILogger log, CancellationToken token)
        {
            var success = true;

            var files = await GetFiles(log, token);

            foreach (var file in Files.Values)
            {
                try
                {
                    log.LogInformation($"Deleting {file.EntityUri.AbsoluteUri}");
                    file.Delete(log, token);
                }
                catch
                {
                    log.LogError($"Unable to delete {file.EntityUri.AbsoluteUri}");
                    success = false;
                }
            }

            return success;
        }

        public ISleetFile Get(string relativePath)
        {
            return Get(GetPath(relativePath));
        }

        public Uri GetPath(string relativePath)
        {
            if (relativePath == null)
            {
                throw new ArgumentNullException(nameof(relativePath));
            }

            if (string.IsNullOrEmpty(FeedSubPath))
            {
                return UriUtility.GetPath(BaseURI, relativePath);
            }
            else
            {
                return UriUtility.GetPath(BaseURI, FeedSubPath, relativePath);
            }
        }

        private static async Task CompleteTask(List<Task> tasks)
        {
            var task = await Task.WhenAny(tasks);
            tasks.Remove(task);
            await task;
        }
    }
}
