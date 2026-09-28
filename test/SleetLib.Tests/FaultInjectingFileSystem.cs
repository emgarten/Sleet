using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using Sleet;

namespace SleetLib.Tests
{
    internal sealed class FaultInjectingFileSystem : ISleetFileSystem
    {
        private readonly ISleetFileSystem _inner;

        public FaultInjectingFileSystem(ISleetFileSystem inner)
        {
            _inner = inner;
        }

        public Uri BaseURI => _inner.BaseURI;

        public LocalCache LocalCache => _inner.LocalCache;

        public ConcurrentDictionary<Uri, ISleetFile> Files => _inner.Files;

        public string FeedSubPath => _inner.FeedSubPath;

        public int CommitCount { get; private set; }

        public Func<ILogger, CancellationToken, Task<bool>> DestroyAsync { get; set; }

        public Func<int, ILogger, CancellationToken, Task<bool>> CommitAsync { get; set; }

        public Action<int> AfterCommit { get; set; }

        public ISleetFile Get(Uri path)
        {
            return _inner.Get(path);
        }

        public ISleetFile Get(string relativePath)
        {
            return _inner.Get(relativePath);
        }

        public Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token)
        {
            return _inner.GetFiles(log, token);
        }

        public Uri GetPath(string relativePath)
        {
            return _inner.GetPath(relativePath);
        }

        public async Task<bool> Commit(ILogger log, CancellationToken token)
        {
            CommitCount++;

            var result = CommitAsync == null
                ? await _inner.Commit(log, token)
                : await CommitAsync(CommitCount, log, token);

            AfterCommit?.Invoke(CommitCount);
            return result;
        }

        public Task<bool> Validate(ILogger log, CancellationToken token)
        {
            return _inner.Validate(log, token);
        }

        public ISleetFileSystemLock CreateLock(ILogger log)
        {
            return _inner.CreateLock(log);
        }

        public Task<bool> Destroy(ILogger log, CancellationToken token)
        {
            return DestroyAsync == null ? _inner.Destroy(log, token) : DestroyAsync(log, token);
        }

        public void Reset()
        {
            _inner.Reset();
        }

        public Task<bool> HasBucket(ILogger log, CancellationToken token)
        {
            return _inner.HasBucket(log, token);
        }

        public Task CreateBucket(ILogger log, CancellationToken token)
        {
            return _inner.CreateBucket(log, token);
        }

        public Task DeleteBucket(ILogger log, CancellationToken token)
        {
            return _inner.DeleteBucket(log, token);
        }
    }
}
