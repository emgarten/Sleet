using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public class PhysicalFile : FileBase
    {
        private readonly FileInfo _sourceFile;

        internal PhysicalFile(PhysicalFileSystem fileSystem, Uri rootPath, Uri displayPath, FileInfo localCacheFile, FileInfo sourceFile)
            : base(fileSystem, rootPath, displayPath, localCacheFile, fileSystem.LocalCache.PerfTracker)
        {
            _sourceFile = sourceFile;
        }

        protected override Task CopyFromSource(ILogger log, CancellationToken token)
        {
            if (File.Exists(_sourceFile.FullName))
            {
                log.LogVerbose($"GET {_sourceFile.FullName}");
                _sourceFile.CopyTo(LocalCacheFile.FullName);
            }

            return Task.FromResult(true);
        }

        protected override Task CopyToSource(ILogger log, CancellationToken token)
        {
            if (File.Exists(LocalCacheFile.FullName))
            {
                log.LogVerbose($"Pushing {_sourceFile.FullName}");

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
                log.LogVerbose($"Removing {_sourceFile.FullName}");
                _sourceFile.Delete();

                if (!Directory.EnumerateFiles(_sourceFile.DirectoryName).Any()
                    && !Directory.EnumerateDirectories(_sourceFile.DirectoryName).Any())
                {
                    // Remove the parent directory if it is now empty
                    log.LogVerbose($"Removing {_sourceFile.DirectoryName}");
                    _sourceFile.Directory.Delete();
                }
            }
            else
            {
                log.LogVerbose($"Skipping {_sourceFile.FullName}");
            }

            return Task.FromResult(true);
        }

        protected override Task<bool> RemoteExists(ILogger log, CancellationToken token)
        {
            return Task.FromResult(File.Exists(_sourceFile.FullName));
        }
    }
}