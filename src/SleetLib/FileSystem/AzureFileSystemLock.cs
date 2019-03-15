using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public class AzureFileSystemLock : FileSystemLockBase
    {
        public const string LockFile = "feedlock";
        public const string LockFileMessage = "feedlock-message";
        private readonly AzureBlobLease _lease;
        private readonly CloudBlockBlob _blob;
        private readonly CloudBlockBlob _messageBlob;
        private Task _keepLockTask = null;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _updateLockMessage;

        public AzureFileSystemLock(CloudBlockBlob blob, CloudBlockBlob messageBlob, ILogger log)
            : base(log)
        {
            _blob = blob ?? throw new ArgumentNullException(nameof(blob));
            _messageBlob = messageBlob ?? throw new ArgumentNullException(nameof(messageBlob));
            _lease = new AzureBlobLease(_blob);
        }

        protected override async Task<Tuple<bool, JObject>> TryObtainLockAsync(string lockMessage, CancellationToken token)
        {
            // Create the feedlock file if it doesn't exist, this will stay around indefinitely. The lock is done
            // by leasing this file.
            await CreateFileIfNotExistsAsync();

            // Try to lease the blob
            var result = await _lease.GetLease();

            if (result)
            {
                // Keep the lease
                if (_keepLockTask == null)
                {
                    _keepLockTask = Task.Run(async () => await KeepLock());
                }

                // For azure blobs the message goes into a separate file.
                var json = GetMessageJson(lockMessage);
                _updateLockMessage = _messageBlob.UploadTextAsync(json.ToString());

                // The message is not needed for success
                return Tuple.Create(result, new JObject());
            }
            else
            {
                // Return a non-successful result along with the message from the other client locking this feed if one exists.
                var json = await GetMessageJson();
                return Tuple.Create(result, json);
            }
        }

        private async Task CreateFileIfNotExistsAsync()
        {
            var exists = await _blob.ExistsAsync();
            if (!exists)
            {
                // Create the feed lock blob if it doesn't exist
                await _blob.UploadTextAsync("{}");
            }
        }

        private async Task<JObject> GetMessageJson()
        {
            try
            {
                var text = await _messageBlob.DownloadTextAsync();
                return JObject.Parse(text);
            }
            catch
            {
                // ignore
            }

            return new JObject();
        }

        public override void Release()
        {
            _cts.Cancel();

            // Wait for the task to complete
            if (_keepLockTask != null)
            {
                _keepLockTask.Wait();
            }

            if (IsLocked)
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

                // Exit lock
                // Make sure writing to the lock finished
                await _updateLockMessage;

                // Delete the message file
                await _messageBlob.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                // Ignore
                Debug.Fail($"KeepLock failed: {ex}");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _cts.Dispose();
        }
    }
}