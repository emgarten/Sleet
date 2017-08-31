using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public class PhysicalFileSystem : FileSystemBase
    {
        /// <summary>
        /// Local root with trailing slash
        /// </summary>
        public string LocalRoot => Path.GetFullPath(Root.LocalPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        public PhysicalFileSystem(LocalCache cache, Uri root)
            : base(cache, root)
        {
        }

        public PhysicalFileSystem(LocalCache cache, Uri root, Uri baseUri)
            : base(cache, root, baseUri)
        {
        }

        public override ISleetFile Get(Uri path)
        {
            if (path == null)
            {
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

        public override Task<bool> Validate(ILogger log, CancellationToken token)
        {
            var dir = new DirectoryInfo(Root.LocalPath);

            if (!dir.Parent.Exists)
            {
                log.LogError($"Local source folder does not exist. Create the folder and try again: {dir.FullName}");

                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public override ISleetFileSystemLock CreateLock(ILogger log)
        {
            return new PhysicalFileSystemLock(Root.LocalPath, log);
        }

        public override async Task<bool> Destroy(ILogger log, CancellationToken token)
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

        public override Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token)
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