using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        public string FeedSubPath { get; protected set; }

        private readonly string[] _roots;

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

            // Ensure the longest root is first to avoid conflicts in StartsWith
            _roots = (new[] { BaseURI.AbsoluteUri, Root.AbsoluteUri })
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(e => e.Length)
                .ThenBy(e => e, StringComparer.Ordinal)
                .ToArray();
        }

        public abstract ISleetFileSystemLock CreateLock(ILogger log);

        public abstract ISleetFile Get(Uri path);

        public abstract Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token);

        public abstract Task<bool> Validate(ILogger log, CancellationToken token);

        public async Task<bool> Commit(ILogger log, CancellationToken token)
        {
            var perfTracker = LocalCache.PerfTracker;

            // Find all files with changes
            var withChanges = Files.Values.Where(e => e.HasChanges).ToList();

            // Order files so that nupkgs are pushed first to help clients avoid
            // missing files during the push.
            withChanges.Sort(new SleetFileComparer());

            if (withChanges.Count > 0)
            {
                var bytes = withChanges.Select(e => e as FileBase)
                    .Where(e => e != null)
                    .Sum(e => e.LocalFileSizeIfExists);

                // Create tasks to run in parallel
                var tasks = withChanges.Select(e => GetCommitFileFunc(e, log, token));

                var message = $"Files committed: {withChanges.Count} Size: {PrintUtility.GetBytesString(bytes)} Total upload time: " + "{0}";
                using (var timer = PerfEntryWrapper.CreateSummaryTimer(message, perfTracker))
                {
                    // Push in parallel
                    await TaskUtils.RunAsync(
                        tasks: tasks,
                        useTaskRun: true,
                        maxThreads: 8,
                        token: token);
                }
            }

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

            return UriUtility.GetPath(BaseURI, relativePath);
        }

        public virtual string GetRelativePath(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            var path = uri.AbsoluteUri;

            // The root can be either the display root (BaseURI) or the actual root.
            foreach (var prefix in _roots)
            {
                // This must have a trailing slash already.
                if (path.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return path.Replace(prefix, string.Empty);
                }
            }

            throw new InvalidOperationException($"Unable to make '{uri.AbsoluteUri}' relative to '{BaseURI}'.");
        }

        /// <summary>
        /// Create a file and add it to Files
        /// </summary>
        protected ISleetFile GetOrAddFile(Uri path, bool caseSensitive, Func<SleetUriPair, ISleetFile> createFile)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var file = Files.GetOrAdd(path, (uri) =>
            {
                return createFile(GetUriPair(path, caseSensitive));
            });

            return file;
        }

        /// <summary>
        /// Inspect a URI and determine the correct root and display URIs
        /// </summary>
        protected SleetUriPair GetUriPair(Uri path, bool caseSensitive)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var isRoot = UriUtility.HasRoot(Root, path, caseSensitive);
            var isDisplay = UriUtility.HasRoot(BaseURI, path, caseSensitive);

            if (!isRoot && !isDisplay)
            {
                throw new InvalidOperationException($"URI does not match the feed root or baseURI: {path.AbsoluteUri}");
            }

            var pair = new SleetUriPair()
            {
                BaseURI = path,
                Root = path
            };

            if (!isRoot)
            {
                pair.Root = UriUtility.ChangeRoot(BaseURI, Root, path);
            }

            if (!isDisplay)
            {
                pair.BaseURI = UriUtility.ChangeRoot(Root, BaseURI, path);
            }

            return pair;
        }
    }
}
