#if !SLEETLEGACY
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using NuGet.Common;
using static Sleet.AmazonS3FileSystemAbstraction;

namespace Sleet
{
    public class AmazonS3FileSystemLock : ISleetFileSystemLock
    {
        public const string LockFile = ".feedlock";

        private readonly string bucketName;
        private readonly IAmazonS3 client;
        private int isLocked;
        private readonly ILogger log;

        public AmazonS3FileSystemLock(IAmazonS3 client, string bucketName, ILogger log)
        {
            this.client = client;
            this.bucketName = bucketName;
            this.log = log;
        }

        public bool IsLocked => isLocked > 0;

        public void Dispose()
        {
            Release();
        }

        public async Task<bool> GetLock(TimeSpan wait, CancellationToken token)
        {
            bool result = false;
            var timer = new Stopwatch();
            timer.Start();
            var lastNotify = TimeSpan.Zero;

            do
            {
                try
                {
                    if (!await FileExistsAsync(client, bucketName, LockFile, token).ConfigureAwait(false))
                    {
                        string fileContent = DateTime.UtcNow.ToString("O", DateTimeFormatInfo.InvariantInfo);
                        await CreateFileAsync(client, bucketName, LockFile, fileContent, token).ConfigureAwait(false);
                        result = true;
                    }
                }
                catch
                {
                    // Ignore and retry
                }

                if (result)
                {
                    continue;
                }

                TimeSpan diff = timer.Elapsed.Subtract(lastNotify);

                if (diff.TotalSeconds > 60)
                {
                    log.LogMinimal($"Waiting to obtain an exclusive lock on the feed.");
                    lastNotify = timer.Elapsed;
                }

                await Task.Delay(100, token).ConfigureAwait(false);
            } while (!result && timer.Elapsed < wait);

            if (result)
            {
                Interlocked.Increment(ref isLocked);
            }
            else
            {
                log.LogError(
                    "Unable to obtain a lock on the feed. If this is an error delete " +
                    $"{bucketName}/{LockFile} and try again.");
            }

            return result;
        }

        public void Release()
        {
            ReleaseAsync(default(CancellationToken)).Wait();
        }

        public async Task ReleaseAsync(CancellationToken token)
        {
            if (!IsLocked)
                return;

            try
            {
                if (await FileExistsAsync(client, bucketName, LockFile, token).ConfigureAwait(false))
                {
                    await RemoveFileAsync(client, bucketName, LockFile, token).ConfigureAwait(false);
                    Interlocked.Exchange(ref isLocked, 0);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning($"Unable to clean up lock: {bucketName}/{LockFile} due to: {ex.Message}");
            }
        }
    }
}
#endif