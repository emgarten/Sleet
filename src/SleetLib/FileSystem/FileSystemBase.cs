using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public abstract class FileSystemBase : ISleetFileSystem
    {
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

            BaseURI = UriUtility.EnsureTrailingSlash(BaseURI);
            Root = UriUtility.EnsureTrailingSlash(Root);
        }

        public abstract ISleetFileSystemLock CreateLock(ILogger log);

        public abstract ISleetFile Get(Uri path);

        public abstract Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token);

        public abstract Task<bool> Validate(ILogger log, CancellationToken token);

        public async Task<bool> Commit(ILogger log, CancellationToken token)
        {
            // Push in parallel
            await TaskUtils.RunAsync(
                tasks: Files.Values.Select(e => GetCommitFileFunc(e, log, token)),
                useTaskRun: true,
                token: token);

            return true;
        }

        private static Func<Task> GetCommitFileFunc(ISleetFile file, ILogger log, CancellationToken token)
        {
            return new Func<Task>(() => file.Push(log, token));
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
    }
}
