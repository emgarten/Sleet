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
            : base(fileSystem, rootPath, displayPath, localCacheFile)
        {
            _sourceFile = sourceFile;
        }

        /// <summary>
        /// Copy/Download file from remote to local. Overwrite a older local version if it exists
        /// NO retry needed/allowed inside here because handled by the caller!
        /// </summary>
        /// <returns>true for success, false if the file does not exists on the source side (and retry does not make sense), Exception in any other case</returns>
        protected override Task<bool> CopyFromSource(ILogger log, CancellationToken token)
        {
            if (File.Exists(_sourceFile.FullName))
            {
                log.LogInformation($"GET {_sourceFile.FullName}");
                _sourceFile.CopyTo(LocalCacheFile.FullName);
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }

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

                if (!Directory.EnumerateFiles(_sourceFile.DirectoryName).Any()
                    && !Directory.EnumerateDirectories(_sourceFile.DirectoryName).Any())
                {
                    // Remove the parent directory if it is now empty
                    log.LogInformation($"Removing {_sourceFile.DirectoryName}");
                    _sourceFile.Directory.Delete();
                }
            }
            else
            {
                log.LogInformation($"Skipping {_sourceFile.FullName}");
            }

            return Task.FromResult(true);
        }

        protected override Task<bool> RemoteExists(ILogger log, CancellationToken token)
        {
            return Task.FromResult(File.Exists(_sourceFile.FullName));
        }
    }
}