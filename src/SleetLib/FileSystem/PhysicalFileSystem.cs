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
        public string LocalRoot
        {
            get
            {
                // no action needed for feed sub paths, it is part of the BaseURI.
                return Path.GetFullPath(Root.LocalPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            }
        }

        public PhysicalFileSystem(LocalCache cache, Uri root)
            : base(cache, root)
        {
            EnsureLocalRoot(root);
        }

        public PhysicalFileSystem(LocalCache cache, Uri root, Uri baseUri, string feedSubPath = null)
            : base(cache, root, baseUri, feedSubPath)
        {
            EnsureLocalRoot(root);
        }

        private static void EnsureLocalRoot(Uri uri)
        {
            if (uri != null && UriUtility.IsHttp(uri))
            {
                throw new ArgumentException("Local feed path cannot be an http URI, use baseURI instead.");
            }
        }

        public override ISleetFile Get(Uri path)
        {
            return GetOrAddFile(path, caseSensitive: false,
                createFile: (pair) => new PhysicalFile(
                    this,
                    pair.Root,
                    pair.BaseURI,
                    LocalCache.GetNewTempPath(),
                    new FileInfo(pair.Root.LocalPath)));
        }

        public override async Task<bool> Validate(ILogger log, CancellationToken token)
        {
            if (!await HasBucket(log, token))
            {
                log.LogError($"Local source folder does not exist. Create the folder and try again: {Root.LocalPath}");

                return false;
            }

            return true;
        }

        public override ISleetFileSystemLock CreateLock(ILogger log)
        {
            var path = Path.Combine(LocalRoot, PhysicalFileSystemLock.LockFile);
            return new PhysicalFileSystemLock(path, log);
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

        public override Task<bool> HasBucket(ILogger log, CancellationToken token)
        {
            return Task.FromResult(Directory.Exists(Root.LocalPath));
        }

        public override Task CreateBucket(ILogger log, CancellationToken token)
        {
            // Create the directory and all needed parent directories
            Directory.CreateDirectory(Root.LocalPath);
            return Task.FromResult(true);
        }

        public override Task DeleteBucket(ILogger log, CancellationToken token)
        {
            Directory.Delete(Root.LocalPath, recursive: true);
            return Task.FromResult(true);
        }
    }
}