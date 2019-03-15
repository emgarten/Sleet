using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Locks a feed/filesystem so that only a single client can update it.
    /// </summary>
    public abstract class FileSystemLockBase : ISleetFileSystemLock
    {
        private volatile bool _isLocked;

        protected ILogger Log { get; set; }

        public FileSystemLockBase(ILogger log)
        {
            Log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Wait between displaying wait messages.
        /// </summary>
        protected virtual TimeSpan DelayBewteenMessages => TimeSpan.FromMinutes(5);

        /// <summary>
        /// Time to wait before trying to get the lock again.
        /// </summary>
        protected virtual TimeSpan WaitBetweenAttempts => TimeSpan.FromSeconds(30);

        public bool IsLocked
        {
            get => _isLocked;
            protected set => _isLocked = value;
        }

        public async Task<bool> GetLock(TimeSpan wait, string lockMessage, CancellationToken token)
        {
            var result = false;
            var timer = Stopwatch.StartNew();
            var lastNotify = Stopwatch.StartNew();
            var notifyDelay = DelayBewteenMessages;
            var waitTime = WaitBetweenAttempts;
            var firstLoop = true;

            if (!_isLocked)
            {
                do
                {
                    var tryResult = await TryObtainLockAsync(lockMessage, token);
                    result = tryResult.Item1;

                    if (!result)
                    {
                        if (lastNotify.Elapsed >= notifyDelay || firstLoop)
                        {
                            var message = $"Waiting to obtain feed lock.";

                            // Print out a message describing how currently has the feed locked.
                            var messageJson = tryResult.Item2 ?? new JObject();
                            if (messageJson.TryGetValue("date", out var dateValue) && messageJson.TryGetValue("message", out var messageValue))
                            {
                                message += $" Feed is locked by: {messageValue.ToObject<string>()} since: {dateValue.ToObject<string>()}";
                            }
                            else
                            {
                                message += " Client holding the lock did not provide a message, it may be using an older version of Sleet.";
                            }

                            if (!string.IsNullOrEmpty(ManualUnlockInstructions))
                            {
                                message += $" {ManualUnlockInstructions}";
                            }

                            Log.LogMinimal(message);
                        }

                        firstLoop = false;
                        await Task.Delay(waitTime);
                    }
                }
                while (!result && timer.Elapsed < wait);

                if (!result)
                {
                    Log.LogError($"Unable to obtain a lock on the feed. Try again later. " + ManualUnlockInstructions);
                }

                _isLocked = result;
            }

            return result;
        }

        /// <summary>
        /// Main lock/read message.
        /// </summary>
        protected abstract Task<Tuple<bool, JObject>> TryObtainLockAsync(string lockMessage, CancellationToken token);

        /// <summary>
        /// Additional instructions for manually unlocking the feed.
        /// </summary>
        protected virtual string ManualUnlockInstructions => string.Empty;

        protected JObject GetMessageJson(string lockMessage)
        {
            return new JObject(
                new JProperty("date", DateTime.UtcNow.ToString("o")),
                new JProperty("message", lockMessage ?? string.Empty));
        }

        public abstract void Release();

        public virtual void Dispose()
        {
            Release();
        }

    }
}
