using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class FileBaseTests
    {
        [Fact]
        public async Task Push_WhenUploadFailsThenSucceeds_RetriesLogsWarningAndClearsChanges()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "package.json");
                file.RetryCountValue = 2;
                file.UploadRetryDelayValue = TimeSpan.Zero;
                file.CopyToSourceAsync = () =>
                {
                    file.UploadAttempts++;
                    if (file.UploadAttempts == 1)
                    {
                        throw new InvalidOperationException("try again");
                    }
                    return Task.CompletedTask;
                };
                await file.Write(new JObject { { "id", "PackageA" } }, log, TestContext.Current.CancellationToken);

                await file.Push(log, TestContext.Current.CancellationToken);

                file.UploadAttempts.Should().Be(2);
                file.HasChanges.Should().BeFalse();
                log.GetMessages().Should().Contain("Failed to upload 'https://example/package.json'. Retrying.");
            }
        }

        [Fact]
        public async Task Push_WhenUploadAlwaysFails_PropagatesAfterRetryCountAndKeepsChanges()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "package.json");
                file.RetryCountValue = 3;
                file.UploadRetryDelayValue = TimeSpan.Zero;
                file.CopyToSourceAsync = () =>
                {
                    file.UploadAttempts++;
                    throw new InvalidOperationException("upload failed");
                };
                await file.Write(new JObject { { "id", "PackageA" } }, log, TestContext.Current.CancellationToken);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => file.Push(log, TestContext.Current.CancellationToken));

                ex.Message.Should().Be("upload failed");
                file.UploadAttempts.Should().Be(3);
                file.HasChanges.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Push_WithRetryCountLessThanOne_TriesOnce()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "package.json");
                file.RetryCountValue = 0;
                file.CopyToSourceAsync = () =>
                {
                    file.UploadAttempts++;
                    throw new InvalidOperationException("upload failed");
                };
                await file.Write(new JObject { { "id", "PackageA" } }, log, TestContext.Current.CancellationToken);

                await Assert.ThrowsAsync<InvalidOperationException>(() => file.Push(log, TestContext.Current.CancellationToken));

                file.UploadAttempts.Should().Be(1);
            }
        }

        [Fact]
        public async Task Push_WhenCancellationHappensDuringRetryDelay_ThrowsOperationCanceledException()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            using (var cts = new CancellationTokenSource())
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "package.json");
                file.RetryCountValue = 2;
                file.UploadRetryDelayValue = TimeSpan.FromSeconds(10);
                file.CopyToSourceAsync = () =>
                {
                    file.UploadAttempts++;
                    cts.Cancel();
                    throw new InvalidOperationException("upload failed");
                };
                await file.Write(new JObject { { "id", "PackageA" } }, log, TestContext.Current.CancellationToken);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => file.Push(log, cts.Token));

                file.UploadAttempts.Should().Be(1);
                file.HasChanges.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Exists_CachesRemoteExistsUntilFileIsDownloaded()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "package.json");
                file.RemoteExistsAsync = () =>
                {
                    file.RemoteExistsAttempts++;
                    return Task.FromResult(true);
                };
                file.CopyFromSourceAsync = () =>
                {
                    file.DownloadAttempts++;
                    file.WriteLocal("{\"id\":\"PackageA\"}");
                    return Task.CompletedTask;
                };

                (await file.Exists(log, TestContext.Current.CancellationToken)).Should().BeTrue();
                (await file.Exists(log, TestContext.Current.CancellationToken)).Should().BeTrue();
                await file.FetchAsync(log, TestContext.Current.CancellationToken);
                File.Delete(file.LocalPath);

                (await file.Exists(log, TestContext.Current.CancellationToken)).Should().BeFalse();
                file.RemoteExistsAttempts.Should().Be(1);
                file.DownloadAttempts.Should().Be(1);
            }
        }

        [Fact]
        public async Task Exists_WhenRemoteDoesNotExist_CachesMissingAsDownloaded()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "missing.json");
                file.RemoteExistsAsync = () =>
                {
                    file.RemoteExistsAttempts++;
                    return Task.FromResult(false);
                };

                (await file.Exists(log, TestContext.Current.CancellationToken)).Should().BeFalse();
                (await file.Exists(log, TestContext.Current.CancellationToken)).Should().BeFalse();

                file.RemoteExistsAttempts.Should().Be(1);
                file.DownloadAttempts.Should().Be(0);
            }
        }

        [Fact]
        public async Task ExistsWithFetch_WhenDownloadFailsThenSucceeds_RetriesAndLogsWarning()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "package.json");
                file.RetryCountValue = 2;
                file.DownloadRetryDelayValue = TimeSpan.Zero;
                file.CopyFromSourceAsync = () =>
                {
                    file.DownloadAttempts++;
                    if (file.DownloadAttempts == 1)
                    {
                        throw new InvalidOperationException("try again");
                    }
                    file.WriteLocal("{\"id\":\"PackageA\"}");
                    return Task.CompletedTask;
                };

                (await file.ExistsWithFetch(log, TestContext.Current.CancellationToken)).Should().BeTrue();

                file.DownloadAttempts.Should().Be(2);
                log.GetMessages().Should().Contain("Failed to sync 'https://example/package.json'. Retrying.");
            }
        }

        [Fact]
        public async Task ExistsWithFetch_WhenDownloadAlwaysFails_PropagatesAfterRetryCount()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "package.json");
                file.RetryCountValue = 2;
                file.DownloadRetryDelayValue = TimeSpan.Zero;
                file.CopyFromSourceAsync = () =>
                {
                    file.DownloadAttempts++;
                    throw new InvalidOperationException("download failed");
                };

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => file.ExistsWithFetch(log, TestContext.Current.CancellationToken));

                ex.Message.Should().Be("download failed");
                file.DownloadAttempts.Should().Be(2);
            }
        }

        [Fact]
        public async Task GetJsonOrNull_ReturnsNullWhenRemoteFileIsMissing()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var file = CreateFile(cache, target.Root, "missing.json");
                file.CopyFromSourceAsync = () => Task.CompletedTask;

                var json = await file.GetJsonOrNull(new TestLogger(), TestContext.Current.CancellationToken);

                json.Should().BeNull();
            }
        }

        [Fact]
        public async Task GetJsonOrNull_ReturnsDownloadedJsonWhenFileExists()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var file = CreateFile(cache, target.Root, "package.json");
                file.CopyFromSourceAsync = () =>
                {
                    file.WriteLocal("{\"id\":\"PackageA\"}");
                    return Task.CompletedTask;
                };

                var json = await file.GetJsonOrNull(new TestLogger(), TestContext.Current.CancellationToken);

                json["id"].ToObject<string>().Should().Be("PackageA");
            }
        }

        [Fact]
        public async Task CopyTo_RespectsOverwriteFlagAndCreatesParentDirectory()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "package.json");
                file.CopyFromSourceAsync = () =>
                {
                    file.DownloadAttempts++;
                    file.WriteLocal("new");
                    return Task.CompletedTask;
                };
                var destination = Path.Combine(target.Root, "out", "package.json");
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.WriteAllText(destination, "old");

                (await file.CopyTo(destination, overwrite: false, log, TestContext.Current.CancellationToken)).Should().BeFalse();
                file.DownloadAttempts.Should().Be(0);
                File.ReadAllText(destination).Should().Be("old");

                (await file.CopyTo(destination, overwrite: true, log, TestContext.Current.CancellationToken)).Should().BeTrue();
                File.ReadAllText(destination).Should().Be("new");

                var nestedDestination = Path.Combine(target.Root, "new", "nested", "package.json");
                (await file.CopyTo(nestedDestination, overwrite: false, log, TestContext.Current.CancellationToken)).Should().BeTrue();
                File.ReadAllText(nestedDestination).Should().Be("new");
            }
        }

        [Fact]
        public void Link_UsesExternalFileAndDeleteRemovesOnlyTheLink()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var log = new TestLogger();
                var file = CreateFile(cache, target.Root, "package.nupkg");
                var externalFile = Path.Combine(target.Root, "external.nupkg");
                File.WriteAllText(externalFile, "package-content");

                file.Link(externalFile, log, TestContext.Current.CancellationToken);
                file.LocalFileSizeIfExists.Should().Be(File.ReadAllBytes(externalFile).Length);

                file.Delete(log, TestContext.Current.CancellationToken);

                File.Exists(externalFile).Should().BeTrue();
                file.LocalFileSizeIfExists.Should().Be(0);
                file.HasChanges.Should().BeTrue();
            }
        }

        [Fact]
        public async Task Invalidate_CausesOperationsThatTouchLocalFileToThrow()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var file = CreateFile(cache, target.Root, "package.json");

                file.Invalidate();

                await Assert.ThrowsAsync<InvalidOperationException>(() => file.FetchAsync(new TestLogger(), TestContext.Current.CancellationToken));
            }
        }

        [Fact]
        public async Task LocalFileSizeIfExists_ReturnsLocalFileLengthOrZero()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache(Path.Combine(target.Root, "cache")))
            {
                var file = CreateFile(cache, target.Root, "package.json");

                file.LocalFileSizeIfExists.Should().Be(0);
                await file.Write(new MemoryStream(new byte[] { 1, 2, 3 }), new TestLogger(), TestContext.Current.CancellationToken);

                file.LocalFileSizeIfExists.Should().Be(3);
            }
        }

        private static PureFileBase CreateFile(LocalCache cache, string root, string relativePath)
        {
            var fileSystem = new PureFileSystem(cache, new Uri("https://example/"));
            var localFile = new FileInfo(Path.Combine(root, "local", Guid.NewGuid().ToString("N") + ".tmp"));
            localFile.Directory.Create();
            return new PureFileBase(fileSystem, new Uri("https://example/" + relativePath), localFile);
        }

        private sealed class PureFileSystem : ISleetFileSystem
        {
            public PureFileSystem(LocalCache localCache, Uri baseUri)
            {
                LocalCache = localCache;
                BaseURI = baseUri;
            }

            public Uri BaseURI { get; }
            public LocalCache LocalCache { get; }
            public ConcurrentDictionary<Uri, ISleetFile> Files { get; } = new ConcurrentDictionary<Uri, ISleetFile>();
            public string FeedSubPath => null;
            public ISleetFile Get(Uri path) => throw new NotImplementedException();
            public ISleetFile Get(string relativePath) => throw new NotImplementedException();
            public Task<IReadOnlyList<ISleetFile>> GetFiles(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Uri GetPath(string relativePath) => new Uri(BaseURI, relativePath);
            public Task<bool> Commit(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task<bool> Validate(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public ISleetFileSystemLock CreateLock(ILogger log) => throw new NotImplementedException();
            public Task<bool> Destroy(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public void Reset() => Files.Clear();
            public Task<bool> HasBucket(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task CreateBucket(ILogger log, CancellationToken token) => throw new NotImplementedException();
            public Task DeleteBucket(ILogger log, CancellationToken token) => throw new NotImplementedException();
        }
        private sealed class PureFileBase : FileBase
        {
            public PureFileBase(ISleetFileSystem fileSystem, Uri uri, FileInfo localCacheFile)
                : base(fileSystem, uri, uri, localCacheFile, NullPerfTracker.Instance)
            {
                CopyFromSourceAsync = () => Task.CompletedTask;
                CopyToSourceAsync = () => Task.CompletedTask;
                RemoteExistsAsync = () => Task.FromResult(File.Exists(LocalPath));
            }

            public Func<Task> CopyFromSourceAsync { get; set; }
            public Func<Task> CopyToSourceAsync { get; set; }
            public Func<Task<bool>> RemoteExistsAsync { get; set; }
            public int DownloadAttempts { get; set; }
            public int RemoteExistsAttempts { get; set; }
            public int UploadAttempts { get; set; }
            public string LocalPath => LocalCacheFile.FullName;
            public int RetryCountValue { set => RetryCount = value; }
            public TimeSpan UploadRetryDelayValue { set => UploadRetryDelay = value; }
            public TimeSpan DownloadRetryDelayValue { set => DownloadRetryDelay = value; }

            public void WriteLocal(string content)
            {
                LocalCacheFile.Directory.Create();
                File.WriteAllText(LocalCacheFile.FullName, content);
                LocalCacheFile.Refresh();
            }

            protected override Task CopyFromSource(ILogger log, CancellationToken token)
            {
                return CopyFromSourceAsync();
            }

            protected override Task CopyToSource(ILogger log, CancellationToken token)
            {
                return CopyToSourceAsync();
            }

            protected override Task<bool> RemoteExists(ILogger log, CancellationToken token)
            {
                return RemoteExistsAsync();
            }
        }
    }
}
