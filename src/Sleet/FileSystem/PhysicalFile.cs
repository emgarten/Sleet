using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;

namespace Sleet
{
    public class PhysicalFile : ISleetFile
    {
        private readonly PhysicalFileSystem _fileSystem;
        private readonly Uri _path;
        private readonly FileInfo _localCacheFile;
        private readonly FileInfo _sourceFile;

        internal PhysicalFile(PhysicalFileSystem fileSystem, Uri path, FileInfo localCacheFile, FileInfo sourceFile)
        {
            _fileSystem = fileSystem;
            _path = path;
            _localCacheFile = localCacheFile;
            _sourceFile = sourceFile;
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
            if (!File.Exists(_localCacheFile.FullName) && File.Exists(_sourceFile.FullName))
            {
                log.LogInformation($"GET {_sourceFile.FullName}");
                _sourceFile.CopyTo(_localCacheFile.FullName);
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
            if (File.Exists(_localCacheFile.FullName))
            {
                log.LogInformation($"Pushing {_sourceFile.FullName}");

                _sourceFile.Directory.Create();

                _localCacheFile.CopyTo(_sourceFile.FullName);
            }
            else if (File.Exists(_sourceFile.FullName))
            {
                log.LogInformation($"Removing {_sourceFile.FullName}");
                _sourceFile.Delete();
            }
            else
            {
                log.LogInformation($"Skipping {_sourceFile.FullName}");
            }

            return Task.FromResult(true);
        }
    }
}
