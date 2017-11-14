using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public class PhysicalFileSystemLock : ISleetFileSystemLock
    {
        private readonly ILogger _log;
        private volatile bool _isLocked;
        public const string LockFile = ".lock";

        public PhysicalFileSystemLock(string path, ILogger log)
        {
            _log = log;
            LockPath = path;
        }

        public bool IsLocked
        {
            get
            {
                return _isLocked;
            }
        }

        public string LockPath { get; }

        public void Dispose()
        {
            Release();
        }

        public Task<bool> GetLock(TimeSpan wait, CancellationToken token)
        {
            var result = false;

            var timer = new Stopwatch();
            timer.Start();

            var lastNotify = TimeSpan.Zero;

            do
            {
                try
                {
                    if (!File.Exists(LockPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(LockPath));

                        using (var stream = new FileStream(LockPath, FileMode.CreateNew))
                        using (var writer = new StreamWriter(stream))
                        {
                            writer.WriteLine(DateTime.UtcNow.ToString("o"));
                        }

                        result = true;
                    }
                }
                catch
                {
                    // Ignore and retry
                }

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
                _log.LogError($"Unable to obtain a lock on the feed. If this is an error delete {LockPath} and try again.");
            }

            _isLocked = result;

            return Task.FromResult<bool>(result);
        }

        public void Release()
        {
            if (IsLocked)
            {
                try
                {
                    if (File.Exists(LockPath))
                    {
                        File.Delete(LockPath);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"Unable to clean up lock: {LockPath} due to: {ex.Message}");
                }
            }
        }
    }
}