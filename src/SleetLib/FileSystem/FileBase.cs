using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public abstract class FileBase : ISleetFile
    {
        private bool _downloaded = false;
        private bool _hasChanges = false;

        protected FileBase(ISleetFileSystem fileSystem, Uri rootPath, Uri displayPath, FileInfo localCacheFile)
        {
            FileSystem = fileSystem;
            RootPath = rootPath;
            EntityUri = displayPath;
            LocalCacheFile = localCacheFile;
        }

        public ISleetFileSystem FileSystem { get; }

        public Uri RootPath { get; }

        protected FileInfo LocalCacheFile { get; }

        public bool HasChanges
        {
            get
            {
                return _hasChanges;
            }
        }

        public Uri EntityUri { get; }

        public async Task<bool> Exists(ILogger log, CancellationToken token)
        {
            await EnsureFile(log, token);

            return File.Exists(LocalCacheFile.FullName);
        }

        public async Task Push(ILogger log, CancellationToken token)
        {
            if (HasChanges)
            {
                for (var i = 0; i < 5; i++)
                {
                    try
                    {
                        // Upload to remote source.
                        await CopyToSource(log, token);

                        break;
                    }
                    catch (Exception ex)
                    {
                        if (i == 4)
                        {
                            throw;
                        }

                        log.LogVerbose(ex.ToString());
                        log.LogWarning($"Failed to upload '{RootPath}'. Retrying.");

                        await Task.Delay(10000);
                    }
                }
            }
        }

        public async Task Write(Stream stream, ILogger log, CancellationToken token)
        {
            _downloaded = true;
            _hasChanges = true;

            using (stream)
            {
                if (File.Exists(LocalCacheFile.FullName))
                {
                    LocalCacheFile.Delete();
                }

                using (var writeStream = File.OpenWrite(LocalCacheFile.FullName))
                {
                    await stream.CopyToAsync(writeStream);
                    writeStream.Seek(0, SeekOrigin.Begin);
                }
            }
        }

        public async Task<JObject> GetJson(ILogger log, CancellationToken token)
        {
            if (await Exists(log, token) == false)
            {
                throw new FileNotFoundException($"File does not exist. Remote: {EntityUri.AbsoluteUri} Local: {LocalCacheFile.FullName}");
            }

            return await JsonUtility.LoadJsonAsync(LocalCacheFile);
        }

        public Task Write(JObject json, ILogger log, CancellationToken token)
        {
            _downloaded = true;
            _hasChanges = true;

            if (File.Exists(LocalCacheFile.FullName))
            {
                LocalCacheFile.Delete();
            }

            return JsonUtility.SaveJsonAsync(LocalCacheFile, json);
        }

        public void Delete(ILogger log, CancellationToken token)
        {
            _hasChanges = true;

            if (File.Exists(LocalCacheFile.FullName))
            {
                File.Delete(LocalCacheFile.FullName);
            }
        }

        protected async Task EnsureFile(ILogger log, CancellationToken token)
        {
            if (!_downloaded)
            {
                for (var i = 0; !_downloaded && i < 5; i++)
                {
                    try
                    {
                        if (File.Exists(LocalCacheFile.FullName))
                        {
                            File.Delete(LocalCacheFile.FullName);
                        }

                        // Download from the remote source.
                        await CopyFromSource(log, token);

                        _downloaded = true;
                    }
                    catch (Exception ex)
                    {
                        if (i == 4)
                        {
                            throw;
                        }

                        log.LogWarning($"Failed to sync '{RootPath}'. Retrying.");
                        log.LogDebug(ex.ToString());

                        await Task.Delay(5000);
                    }
                }
            }
        }

        protected abstract Task CopyFromSource(ILogger log, CancellationToken token);

        protected abstract Task CopyToSource(ILogger log, CancellationToken token);

        public async Task<Stream> GetStream(ILogger log, CancellationToken token)
        {
            await EnsureFile(log, token);

            if (LocalCacheFile.Exists)
            {
                return LocalCacheFile.OpenRead();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Copy the file to the destination path.
        /// </summary>
        /// <returns>True if the file was successfully copied.</returns>
        public async Task<bool> CopyTo(string path, bool overwrite, ILogger log, CancellationToken token)
        {
            var pathInfo = new FileInfo(path);

            if (!overwrite && pathInfo.Exists)
            {
                return false;
            }

            if (await Exists(log, token))
            {
                // Create the parent dir
                pathInfo.Directory.Create();

                // Copy the file
                LocalCacheFile.CopyTo(pathInfo.FullName, overwrite);

                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return EntityUri.AbsoluteUri;
        }

        public Task FetchAsync(ILogger log, CancellationToken token)
        {
            return EnsureFile(log, token);
        }
    }
}