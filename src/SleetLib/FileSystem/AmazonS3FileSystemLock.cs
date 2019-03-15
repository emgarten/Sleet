#if !SLEETLEGACY
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using static Sleet.AmazonS3FileSystemAbstraction;

namespace Sleet
{
    public class AmazonS3FileSystemLock : FileSystemLockBase
    {
        public const string LockFile = ".feedlock";

        private readonly string bucketName;
        private readonly IAmazonS3 client;

        public AmazonS3FileSystemLock(IAmazonS3 client, string bucketName, ILogger log)
            : base(log)
        {
            this.client = client;
            this.bucketName = bucketName;
        }

        protected override async Task<Tuple<bool, JObject>> TryObtainLockAsync(string lockMessage, CancellationToken token)
        {
            var result = false;
            var json = new JObject();

            try
            {
                if (await FileExistsAsync(client, bucketName, LockFile, token).ConfigureAwait(false))
                {
                    // Read the existing message
                    json = await GetExistingMessage(json, token);
                }
                else
                {
                    // Create a new lock
                    json = GetMessageJson(lockMessage);
                    await CreateFileAsync(client, bucketName, LockFile, json.ToString(), token).ConfigureAwait(false);
                    result = true;
                }
            }
            catch
            {
                // Ignore and retry
            }

            return Tuple.Create(result, json);
        }

        private async Task<JObject> GetExistingMessage(JObject json, CancellationToken token)
        {
            using (var ms = new MemoryStream())
            {
                await DownloadFileAsync(client, bucketName, LockFile, ms, token);
                ms.Position = 0;
                json = await JsonUtility.LoadJsonAsync(ms);
            }

            return json;
        }

        public override void Release()
        {
            ReleaseAsync(default(CancellationToken)).Wait();
        }

        protected override string ManualUnlockInstructions => $"Delete {bucketName}/{LockFile} to forcibly unlock the feed.";

        public async Task ReleaseAsync(CancellationToken token)
        {
            if (IsLocked)
            {
                try
                {
                    if (await FileExistsAsync(client, bucketName, LockFile, token).ConfigureAwait(false))
                    {
                        await RemoveFileAsync(client, bucketName, LockFile, token).ConfigureAwait(false);
                        IsLocked = false;
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Unable to clean up lock: {bucketName}/{LockFile} due to: {ex.Message}");
                }
            }
        }

    }
}
#endif