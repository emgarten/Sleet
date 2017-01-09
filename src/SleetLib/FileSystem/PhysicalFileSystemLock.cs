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
        private const string LockFile = ".lock";
        private readonly string _lockPath;

        public PhysicalFileSystemLock(string root, ILogger log)
        {
            _log = log;
            _lockPath = Path.Combine(root, LockFile);
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
                    if (!File.Exists(_lockPath))
                    {
                        using (var stream = new FileStream(_lockPath, FileMode.CreateNew))
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
                _log.LogError($"Unable to obtain a lock on the feed. If this is an error delete {_lockPath} and try again.");
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
                    if (File.Exists(_lockPath))
                    {
                        File.Delete(_lockPath);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"Unable to clean up lock: {_lockPath} due to: {ex.Message}");
                }
            }
        }
    }
}