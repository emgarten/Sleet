using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    public class PhysicalFileSystemLock : FileSystemLockBase
    {
        public const string LockFile = ".lock";

        public PhysicalFileSystemLock(string path, ILogger log)
            : base(log)
        {
            LockPath = path;
        }

        public string LockPath { get; }

        protected override TimeSpan WaitBetweenAttempts => TimeSpan.FromMilliseconds(200);

        protected override async Task<Tuple<bool, JObject>> TryObtainLockAsync(string lockMessage, CancellationToken token)
        {
            var result = false;
            var json = new JObject();

            try
            {
                if (File.Exists(LockFile))
                {
                    // Read message from existing lock file
                    json = await JsonUtility.LoadJsonAsync(LockPath);
                }
                else
                {
                    // Obtain the lock
                    Directory.CreateDirectory(Path.GetDirectoryName(LockPath));

                    json = GetMessageJson(lockMessage);
                    json.Add(new JProperty("pid", Process.GetCurrentProcess().Id));

                    using (var stream = new FileStream(LockPath, FileMode.CreateNew))
                    {
                        await JsonUtility.WriteJsonAsync(json, stream);
                    }

                    result = true;
                }
            }
            catch
            {
                // Ignore and retry
            }

            return Tuple.Create(result, json);
        }

        protected override string ManualUnlockInstructions => $"Delete {LockFile} to forcibly unlock the feed.";

        public override void Release()
        {
            if (IsLocked)
            {
                var timer = Stopwatch.StartNew();
                var success = false;
                var max = new TimeSpan(0, 1, 0);
                var message = string.Empty;

                while (!success && timer.Elapsed < max)
                {
                    try
                    {
                        if (File.Exists(LockPath))
                        {
                            File.Delete(LockPath);
                            success = true;
                            IsLocked = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        message = $"Unable to clean up lock: {LockPath} due to: {ex.Message}";
                        Thread.Sleep(100);
                    }
                }

                if (!success && !string.IsNullOrEmpty(message))
                {
                    Log.LogWarning(message);
                }
            }
        }
    }
}