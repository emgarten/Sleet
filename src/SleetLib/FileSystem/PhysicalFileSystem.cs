using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public class PhysicalFileSystem : ISleetFileSystem
    {
        private readonly Uri _baseUri;
        private readonly Uri _root;
        private readonly LocalCache _cache;
        private readonly ConcurrentDictionary<Uri, ISleetFile> _files;

        public PhysicalFileSystem(LocalCache cache, Uri root)
            : this(cache, root, root)
        {
        }

        public PhysicalFileSystem(LocalCache cache, Uri root, Uri baseUri)
        {
            _baseUri = UriUtility.EnsureTrailingSlash(baseUri);
            _root = UriUtility.EnsureTrailingSlash(root);
            _cache = cache;
            _files = new ConcurrentDictionary<Uri, ISleetFile>();
        }

        public ConcurrentDictionary<Uri, ISleetFile> Files
        {
            get
            {
                return _files;
            }
        }

        public LocalCache LocalCache
        {
            get
            {
                return _cache;
            }
        }

        /// <summary>
        /// Base uri written for @id
        /// </summary>
        public Uri BaseURI
        {
            get
            {
                return _baseUri;
            }
        }

        /// <summary>
        /// Actual root path
        /// </summary>
        public Uri Root
        {
            get
            {
                return _root;
            }
        }

        /// <summary>
        /// Local root with trailing slash
        /// </summary>
        public string LocalRoot
        {
            get
            {
                return Path.GetFullPath(Root.LocalPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            }
        }

        public ISleetFile Get(string relativePath)
        {
            return Get(GetPath(relativePath));
        }

        public ISleetFile Get(Uri path)
        {
            if (path == null)
            {
                Debug.Fail("bad path");
                throw new ArgumentNullException(nameof(path));
            }

            if (!path.AbsoluteUri.StartsWith(BaseURI.AbsoluteUri))
            {
                throw new ArgumentException(string.Format("Base uri does not match the file system. Url: {0}, Expecting: {1}", path.AbsoluteUri, BaseURI.AbsoluteUri));
            }

            var file = Files.GetOrAdd(path, (uri) =>
            {
                var rootUri = uri;
                var displayUri = uri;

                if (!UriUtility.HasRoot(Root, rootUri))
                {
                    rootUri = UriUtility.ChangeRoot(BaseURI, Root, uri);
                }

                if (!UriUtility.HasRoot(BaseURI, displayUri))
                {
                    displayUri = UriUtility.ChangeRoot(Root, BaseURI, uri);
                }

                return new PhysicalFile(
                    this,
                    rootUri,
                    displayUri,
                    LocalCache.GetNewTempPath(),
                    new FileInfo(rootUri.LocalPath));
            });

            return file;
        }

        public Uri GetPath(string relativePath)
        {
            if (relativePath == null)
            {
                Debug.Fail("bad path");
                throw new ArgumentNullException(nameof(relativePath));
            }

            return UriUtility.GetPath(BaseURI, relativePath);
        }

        public async Task<bool> Commit(ILogger log, CancellationToken token)
        {
            foreach (var file in Files.Values)
            {
                await file.Push(log, token);
            }

            return true;
        }

        public Task<bool> Validate(ILogger log, CancellationToken token)
        {
            var dir = new DirectoryInfo(Root.LocalPath);

            if (!dir.Parent.Exists)
            {
                log.LogError($"Local source folder does not exist. Create the folder and try again: {dir.FullName}");

                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public ISleetFileSystemLock CreateLock(ILogger log)
        {
            return new PhysicalFileSystemLock(Root.LocalPath, log);
        }

        public async Task<bool> Destroy(ILogger log, CancellationToken token)
        {
            var success = true;

            // Clear all files except the lock file
            foreach (var file in await GetFiles(log, token))
            {
                try
                {
                    log.LogVerbose($"Deleting {file.EntityUri.AbsoluteUri}");
                    file.Delete(log, token);
                }
                catch
                {
                    log.LogError($"Unable to delete {file.EntityUri.AbsoluteUri}");
                    success = false;
                }
            }

            // Clear any remaining directories
            foreach (var dir in Directory.GetDirectories(LocalRoot))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    log.LogError($"Unable to delete directory {dir}");
                    success = false;
                }
            }

            return success;
        }

        public Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token)
        {
            // Return all files except .lock
            return Task.FromResult<IReadOnlyList<ISleetFile>>(
                Directory.GetFiles(LocalRoot, "*", SearchOption.AllDirectories)
                    .Where(path => !StringComparer.Ordinal.Equals(PhysicalFileSystemLock.LockFile, Path.GetFileName(path)))
                    .Select(UriUtility.CreateUri)
                    .Select(Get)
                    .ToList());
        }
    }
}