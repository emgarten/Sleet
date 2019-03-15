using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sleet
{
    public interface ISleetFileSystemLock : IDisposable
    {
        /// <summary>
        /// Enter the lock. Returns true if successful.
        /// </summary>
        Task<bool> GetLock(TimeSpan wait, string lockMessage, CancellationToken token);

        /// <summary>
        /// True if locked.
        /// </summary>
        bool IsLocked { get; }

        /// <summary>
        /// Release lock.
        /// </summary>
        void Release();
    }
}