using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sleet
{
    public class MemoryFileSystem : ISleetFileSystem
    {
        private readonly Uri _root;
        private readonly LocalCache _cache;

        public MemoryFileSystem(LocalCache cache, Uri root)
        {
            _root = root;
            _cache = cache;
            Files = new ConcurrentDictionary<Uri, ISleetFile>();
        }

        public LocalCache LocalCache
        {
            get
            {
                return _cache;
            }
        }

        public Uri Root
        {
            get
            {
                return _root;
            }
        }

        public ISleetFile Create(Uri path)
        {
            return new MemoryFile(this, path, LocalCache.GetNewTempPath());
        }

        public ISleetFile Get(Uri path)
        {
            throw new NotImplementedException();
        }

        public ISleetFile Get(string relativePath)
        {
            throw new NotImplementedException();
        }

        public Uri GetPath(string relativePath)
        {
            return new Uri(Root, relativePath);
        }

        public ConcurrentDictionary<Uri, ISleetFile> Files { get; }
    }
}
