using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;

namespace Sleet
{
    public class MemoryFile : ISleetFile
    {
        private readonly MemoryFileSystem _fileSystem;
        private readonly Uri _path;
        private readonly FileInfo _localCacheFile;
        private bool _isLoaded;

        internal MemoryFile(MemoryFileSystem fileSystem, Uri path, FileInfo localCacheFile)
        {
            _fileSystem = fileSystem;
            _path = path;
            _localCacheFile = localCacheFile;
        }

        public ISleetFileSystem FileSystem
        {
            get
            {
                return _fileSystem;
            }
        }

        public Uri Path
        {
            get
            {
                return _path;
            }
        }

        public async Task<bool> Exists(ILogger log, CancellationToken token)
        {
            var file = await GetLocal(log, token);
            return file.Exists;
        }

        public Task Get(ILogger log, CancellationToken token)
        {
            if (!_isLoaded)
            {
                lock (_localCacheFile)
                {
                    if (!_isLoaded)
                    {
                        if (_fileSystem.Files.ContainsKey(Path))
                        {
                            using (var localStream = _localCacheFile.OpenWrite())
                            {
                                _fileSystem.Files[Path];
                                    
                                    .CopyTo(localStream);
                            }
                        }

                        _isLoaded = true;
                    }
                }
            }

            return Task.FromResult(true);
        }

        public async Task<FileInfo> GetLocal(ILogger log, CancellationToken token)
        {
            await Get(log, token);

            return _localCacheFile;
        }

        public Task Push(ILogger log, CancellationToken token)
        {
            lock (_localCacheFile)
            {
                if (_localCacheFile.Exists)
                {
                    using (var localStream = _localCacheFile.OpenRead())
                    {
                        var memoryStream = new MemoryStream();

                        localStream.CopyTo(memoryStream);

                        _fileSystem.Files.AddOrUpdate(Path, (p) => memoryStream, (p, v) => memoryStream);
                        _fileSystem.Files[Path] = memoryStream;
                    }
                }

                _isLoaded = true;
            }

            return Task.FromResult(true);
        }
    }
}
