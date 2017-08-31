using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Common file operations.
    /// </summary>
    public abstract class FileBase : ISleetFile
    {
        /// <summary>
        /// File system tracking the file.
        /// </summary>
        public ISleetFileSystem FileSystem { get; }

        /// <summary>
        /// Root path.
        /// </summary>
        public Uri RootPath { get; }

        /// <summary>
        /// Remote feed URI.
        /// </summary>
        public Uri EntityUri { get; }

        /// <summary>
        /// True if the file has been modified.
        /// </summary>
        public bool HasChanges { get; private set; }

        /// <summary>
        /// True if the file was downloaded at some point.
        /// This will be true even if the file was deleted.
        /// </summary>
        protected bool IsDownloaded { get; private set; }

        /// <summary>
        /// True if the file exists. This is for internally
        /// tracking if the remote source contains the file
        /// before it is downloaded.
        /// </summary>
        protected bool? RemoteExistsCacheValue { get; private set; }

        /// <summary>
        /// Temp file on disk representing the remote file.
        /// </summary>
        protected FileInfo LocalCacheFile { get; }

        /// <summary>
        /// Retry count for failures.
        /// </summary>
        protected int RetryCount { get; set; } = 5;

        protected FileBase(ISleetFileSystem fileSystem, Uri rootPath, Uri displayPath, FileInfo localCacheFile)
        {
            FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            EntityUri = displayPath ?? throw new ArgumentNullException(nameof(displayPath));
            LocalCacheFile = localCacheFile ?? throw new ArgumentNullException(nameof(localCacheFile));
        }

        /// <summary>
        /// True if the file exists.
        /// </summary>
        public async Task<bool> Exists(ILogger log, CancellationToken token)
        {
            // If the file was aleady downloaded then the local disk is the authority.
            if (IsDownloaded)
            {
                return File.Exists(LocalCacheFile.FullName);
            }

            // If the file was not downloaded check the remote source if needed.
            if (!RemoteExistsCacheValue.HasValue)
            {
                // Check the remote source
                RemoteExistsCacheValue = await RemoteExists(log, token);

                // If the file doesn't exist then it can be marked as downloaded
                // to avoid an extra request later if Fetch is called.
                if (!RemoteExistsCacheValue.Value)
                {
                    IsDownloaded = true;
                }
            }

            // Use the existing check.
            return RemoteExistsCacheValue.Value;
        }

        /// <summary>
        /// Fetch a file then check if it exists. This is optimized for scenarios
        /// where it is known that the file will be used if it exists.
        /// </summary>
        public async Task<bool> ExistsWithFetch(ILogger log, CancellationToken token)
        {
            await FetchAsync(log, token);
            return await Exists(log, token);
        }

        public async Task Push(ILogger log, CancellationToken token)
        {
            if (HasChanges)
            {
                var retry = Math.Max(RetryCount, 1);

                for (var i = 0; i < retry; i++)
                {
                    try
                    {
                        // Upload to remote source.
                        await CopyToSource(log, token);

                        // The file no longer has changes.
                        HasChanges = false;

                        break;
                    }
                    catch (Exception ex) when (i < (retry - 1))
                    {
                        await log.LogAsync(LogLevel.Debug, ex.ToString());
                        await log.LogAsync(LogLevel.Warning, $"Failed to upload '{RootPath}'. Retrying.");
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve json file.
        /// </summary>
        public async Task<JObject> GetJson(ILogger log, CancellationToken token)
        {
            await EnsureFileOrThrow(log, token);

            return await JsonUtility.LoadJsonAsync(LocalCacheFile);
        }

        /// <summary>
        /// Retrieve json file if it exists.
        /// </summary>
        public async Task<JObject> GetJsonOrNull(ILogger log, CancellationToken token)
        {
            JObject json = null;

            if (await ExistsWithFetch(log, token))
            {
                json = await JsonUtility.LoadJsonAsync(LocalCacheFile);
            }

            return json;
        }

        /// <summary>
        /// Write json to the file.
        /// </summary>
        public Task Write(JObject json, ILogger log, CancellationToken token)
        {
            // Remove the file if it exists
            Delete(log, token);

            // Write out json to the file.
            return JsonUtility.SaveJsonAsync(LocalCacheFile, json);
        }

        /// <summary>
        /// Write a stream to the file.
        /// </summary>
        public async Task Write(Stream stream, ILogger log, CancellationToken token)
        {
            // Remove the file if it exists
            Delete(log, token);

            using (stream)
            using (var writeStream = File.OpenWrite(LocalCacheFile.FullName))
            {
                await stream.CopyToAsync(writeStream);
            }
        }

        /// <summary>
        /// Delete a file from the feed.
        /// </summary>
        public void Delete(ILogger log, CancellationToken token)
        {
            IsDownloaded = true;
            HasChanges = true;

            if (File.Exists(LocalCacheFile.FullName))
            {
                File.Delete(LocalCacheFile.FullName);
            }
        }

        /// <summary>
        /// Ensure that the file exists on disk if it exists.
        /// </summary>
        protected async Task EnsureFile(ILogger log, CancellationToken token)
        {
            if (!IsDownloaded)
            {
                var retry = Math.Max(RetryCount, 1);

                for (var i = 0; !IsDownloaded && i < retry; i++)
                {
                    try
                    {
                        if (File.Exists(LocalCacheFile.FullName))
                        {
                            File.Delete(LocalCacheFile.FullName);
                        }

                        // Download from the remote source.
                        await CopyFromSource(log, token);

                        IsDownloaded = true;
                    }
                    catch (Exception ex) when (i < (retry - 1))
                    {
                        await log.LogAsync(LogLevel.Debug, ex.ToString());
                        await log.LogAsync(LogLevel.Warning, $"Failed to sync '{RootPath}'. Retrying.");
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
            }
        }

        /// <summary>
        /// Ensure that the file is downloaded to disk. If it does not exist throw.
        /// </summary>
        protected async Task EnsureFileOrThrow(ILogger log, CancellationToken token)
        {
            await EnsureFile(log, token);

            if (!File.Exists(LocalCacheFile.FullName))
            {
                throw new FileNotFoundException($"File does not exist. Remote: {EntityUri.AbsoluteUri} Local: {LocalCacheFile.FullName}");
            }
        }

        /// <summary>
        /// Returns the stream the file exists. Otherwise throws.
        /// </summary>
        public async Task<Stream> GetStream(ILogger log, CancellationToken token)
        {
            await EnsureFileOrThrow(log, token);

            return File.OpenRead(LocalCacheFile.FullName);
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

            // Download the file if needed.
            await EnsureFile(log, token);

            // Check if the local copy exists
            if (File.Exists(LocalCacheFile.FullName))
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

        /// <summary>
        /// Ensure that the file has been downloaded to disk if it exists.
        /// </summary>
        public Task FetchAsync(ILogger log, CancellationToken token)
        {
            return EnsureFile(log, token);
        }

        /// <summary>
        /// Download a file to disk.
        /// </summary>
        protected abstract Task CopyFromSource(ILogger log, CancellationToken token);

        /// <summary>
        /// Upload a file.
        /// </summary>
        protected abstract Task CopyToSource(ILogger log, CancellationToken token);

        /// <summary>
        /// True if the file exists in the source.
        /// </summary>
        protected abstract Task<bool> RemoteExists(ILogger log, CancellationToken token);
    }
}