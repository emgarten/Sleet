using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Common;

namespace Sleet
{
    public class AzureFileSystemLock : ISleetFileSystemLock
    {
        private readonly ILogger _log;
        private volatile bool _isLocked;
        public const string LockFile = "feedlock";
        private readonly AzureBlobLease _lease;
        private readonly CloudBlockBlob _blob;
        private Task _keepLockTask;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public AzureFileSystemLock(CloudBlockBlob blob, ILogger log)
        {
            _log = log;
            _blob = blob;
            _lease = new AzureBlobLease(_blob);
        }

        public bool IsLocked
        {
            get
            {
                return _isLocked;
            }
        }

        public async Task<bool> GetLock(TimeSpan wait, CancellationToken token)
        {
            var result = false;
            var timer = Stopwatch.StartNew();
            var lastNotify = Stopwatch.StartNew();
            var notifyDelay = TimeSpan.FromMinutes(5);
            var waitTime = TimeSpan.FromSeconds(30);
            var firstLoop = true;

            if (!_isLocked)
            {
                var exists = await _blob.ExistsAsync();
                if (!exists)
                {
                    // Create the feed lock blob if it doesn't exist
                    var bytes = Encoding.UTF8.GetBytes("feedlock");
                    await _blob.UploadFromByteArrayAsync(bytes, 0, bytes.Length);
                }

                do
                {
                    result = await _lease.GetLease();

                    if (!result)
                    {
                        if (lastNotify.Elapsed >= notifyDelay || firstLoop)
                        {
                            _log.LogMinimal($"Waiting to obtain an exclusive lock on the feed.");
                        }

                        firstLoop = false;
                        await Task.Delay(waitTime);
                    }
                }
                while (!result && timer.Elapsed < wait);

                if (!result)
                {
                    _log.LogError($"Unable to obtain a lock on the feed. Try again later.");
                }
                else if (_keepLockTask == null)
                {
                    _keepLockTask = Task.Run(async () => await KeepLock());
                }

                _isLocked = result;
            }

            return result;
        }

        public void Release()
        {
            _cts.Cancel();

            // Wait for the task to complete
            if (_keepLockTask != null)
            {
                _keepLockTask.Wait();
            }

            if (_isLocked)
            {
                _lease.Release();
            }
        }

        private async Task KeepLock()
        {
            var renewTime = TimeSpan.FromSeconds(15);

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        await _lease.Renew();
                        await Task.Delay(renewTime, _cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Ignore
                    }
                    catch (Exception ex)
                    {
                        // Ignore
                        Debug.Fail($"KeepLock failed: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore
                Debug.Fail($"KeepLock failed: {ex}");
            }
        }

        public void Dispose()
        {
            Release();

            _cts.Dispose();
        }
    }
}