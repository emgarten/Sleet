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
        private const string LockFile = "feedlock";
        private readonly AzureBlobLease _lease;
        private readonly CloudBlockBlob _blob;

        public AzureFileSystemLock(CloudBlobContainer container, ILogger log)
        {
            _log = log;

            _blob = container.GetBlockBlobReference(LockFile);
            _lease = new AzureBlobLease(_blob);
        }

        public bool IsLocked
        {
            get
            {
                return _isLocked;
            }
        }

        public void Dispose()
        {
            Release();
        }

        public async Task<bool> GetLock(TimeSpan wait, CancellationToken token)
        {
            var result = false;

            var exists = await _blob.ExistsAsync();
            if (!exists)
            {
                // Create the feed lock blob if it doesn't exist
                var bytes = Encoding.UTF8.GetBytes("feed lock");
                await _blob.UploadFromByteArrayAsync(bytes, 0, bytes.Length);
            }

            var timer = new Stopwatch();
            timer.Start();

            var lastNotify = TimeSpan.Zero;

            do
            {
                result = await _lease.GetLease();

                if (!result)
                {
                    var diff = timer.Elapsed.Subtract(lastNotify);

                    if (diff.TotalSeconds > 60)
                    {
                        _log.LogMinimal($"Waiting to obtain an exclusive lock on the feed.");
                    }

                    Thread.Sleep(100);
                }
            }
            while (!result && timer.Elapsed < wait);

            if (!result)
            {
                _log.LogError($"Unable to obtain a lock on the feed. Try again later.");
            }

            _isLocked = result;

            return result;
        }

        public void Release()
        {
            if (_isLocked)
            {
                _lease.Release();
            }
        }
    }
}
