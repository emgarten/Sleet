using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;

namespace Sleet
{
    public class PhysicalFile : FileBase
    {
        private readonly FileInfo _sourceFile;

        internal PhysicalFile(PhysicalFileSystem fileSystem, Uri path, FileInfo localCacheFile, FileInfo sourceFile)
            : base(fileSystem, path, localCacheFile)
        {
            _sourceFile = sourceFile;
        }

        protected override Task CopyFromSource(ILogger log, CancellationToken token)
        {
            if (File.Exists(_sourceFile.FullName))
            {
                log.LogInformation($"GET {_sourceFile.FullName}");
                _sourceFile.CopyTo(LocalCacheFile.FullName);
            }

            return Task.FromResult(true);
        }

        protected override Task CopyToSource(ILogger log, CancellationToken token)
        {
            if (File.Exists(LocalCacheFile.FullName))
            {
                log.LogInformation($"Pushing {_sourceFile.FullName}");

                _sourceFile.Directory.Create();

                var tmp = _sourceFile.FullName + ".tmp";

                if (File.Exists(tmp))
                {
                    // Clean up tmp file
                    File.Delete(tmp);
                }

                LocalCacheFile.CopyTo(tmp);

                if (File.Exists(_sourceFile.FullName))
                {
                    // Clean up old file
                    File.Delete(_sourceFile.FullName);
                }

                File.Move(tmp, _sourceFile.FullName);
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
