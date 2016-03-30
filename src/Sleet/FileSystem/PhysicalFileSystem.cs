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

            var file = Files.GetOrAdd(path, (uri) =>
            {
                var rootUri = UriUtility.ChangeRoot(_baseUri, _root, uri);

                return new PhysicalFile(
                    this,
                    rootUri,
                    uri,
                    LocalCache.GetNewTempPath(),
                    new FileInfo(path.LocalPath));
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

            relativePath = relativePath.TrimStart(new char[] { '\\', '/' });

            var combined = new Uri(Path.GetFullPath(Path.Combine(BaseURI.LocalPath, relativePath)));
            return combined;
        }

        public async Task<bool> Commit(ILogger log, CancellationToken token)
        {
            foreach (var file in Files.Values)
            {
                await file.Push(log, token);
            }

            return true;
        }
    }
}
