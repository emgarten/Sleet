using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public class AzureFileSystemLock : FileSystemLockBase
    {
        public const string LockFile = "feedlock";
        public const string LockFileMessage = "feedlock-message";
        private readonly AzureBlobLease _lease;
        private readonly BlobClient _blob;
        private readonly BlobClient _messageBlob;
        private Task _keepLockTask = null;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _updateLockMessage;

        public AzureFileSystemLock(BlobClient blob, BlobClient messageBlob, ILogger log)
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
                    _keepLockTask = Task.Run(async () => await KeepLock(), token);
                }

                // For azure blobs the message goes into a separate file.
                var json = GetMessageJson(lockMessage);
                _updateLockMessage = _messageBlob.UploadAsync(BinaryData.FromString(json.ToString()), overwrite: true, token);

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
                await _blob.UploadAsync(BinaryData.FromString("{}"));
            }
        }

        private async Task<JObject> GetMessageJson()
        {
            try
            {
                var blobDownloadContent = await _messageBlob.DownloadContentAsync();
                var text = blobDownloadContent.Value.Content.ToString();
                return JObject.Parse(text);
            }
            catch (Exception ex)
            {
                // ignore
                ExceptionUtilsSleetLib.LogException(ex, Log, LogLevel.Debug);
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
                        Log.LogWarning("Failed to renew lock on feed. If another client takes the lock conflicts could occur.");
                        ExceptionUtilsSleetLib.LogException(ex, Log, LogLevel.Warning);
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
                Log.LogWarning("Unable to clear lock message");
                ExceptionUtilsSleetLib.LogException(ex, Log, LogLevel.Warning);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _cts.Dispose();
        }
    }
}
