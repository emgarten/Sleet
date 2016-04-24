using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;

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
            _baseUri = new Uri(baseUri.AbsoluteUri.TrimEnd(new char[] { '/', '\\' }) + Path.DirectorySeparatorChar);
            _root = new Uri(root.AbsoluteUri.TrimEnd(new char[] { '/', '\\' }) + Path.DirectorySeparatorChar);
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
    }
}
