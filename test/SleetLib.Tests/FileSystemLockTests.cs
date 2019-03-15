using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class FileSystemLockTests
    {
        [Fact]
        public async Task FileSystemLock_VerifyMessageShownInLog()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var log2 = new TestLogger();
                var fileSystem1 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var lockMessage = Guid.NewGuid().ToString();

                await InitCommand.RunAsync(settings, fileSystem1, log);

                var lockObj1 = await SourceUtility.VerifyInitAndLock(settings, fileSystem1, lockMessage, log, CancellationToken.None);
                lockObj1.IsLocked.Should().BeTrue();

                var lockObj2Task = Task.Run(async () => await SourceUtility.VerifyInitAndLock(settings, fileSystem2, lockMessage, log2, CancellationToken.None));

                while (!log2.GetMessages().Contains($"Feed is locked by: {lockMessage}"))
                {
                    await Task.Delay(10);
                }

                lockObj1.Release();
                var lockObj2 = await lockObj2Task;

                while (!lockObj2.IsLocked)
                {
                    await Task.Delay(10);
                }

                lockObj1.IsLocked.Should().BeFalse();
                lockObj2.IsLocked.Should().BeTrue();
            }
        }

        [Fact]
        public async Task FileSystemLock_VerifyMessage()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                var lockMessage = Guid.NewGuid().ToString();

                await InitCommand.RunAsync(settings, fileSystem, log);

                var lockObj = await SourceUtility.VerifyInitAndLock(settings, fileSystem, lockMessage, log, CancellationToken.None);
                lockObj.IsLocked.Should().BeTrue();

                var path = Path.Combine(target.Root, ".lock");
                var json = JObject.Parse(File.ReadAllText(path));

                json["message"].ToString().Should().Be(lockMessage);
                json["date"].ToString().Should().NotBeNullOrEmpty();
                json["pid"].ToString().Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public async Task FileSystemLock_VerifyMessageFromSettings()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));
                var settings = new LocalSettings();
                settings.FeedLockMessage = "FROMSETTINGS!!";
                var lockMessage = Guid.NewGuid().ToString();

                await InitCommand.RunAsync(settings, fileSystem, log);

                var lockObj = await SourceUtility.VerifyInitAndLock(settings, fileSystem, lockMessage, log, CancellationToken.None);
                lockObj.IsLocked.Should().BeTrue();

                var path = Path.Combine(target.Root, ".lock");
                var json = JObject.Parse(File.ReadAllText(path));

                json["message"].ToString().Should().Be("FROMSETTINGS!!");
                json["date"].ToString().Should().NotBeNullOrEmpty();
                json["pid"].ToString().Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public async Task FileSystemLock_SameFileSystemAsync()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var lock1 = fileSystem.CreateLock(log);
                var lock2 = fileSystem.CreateLock(log);
                var lock3 = fileSystem.CreateLock(log);

                // Act
                var lock1Result = await lock1.GetLock(TimeSpan.FromSeconds(1), string.Empty, CancellationToken.None);
                var lock2Result = await lock2.GetLock(TimeSpan.FromSeconds(1), string.Empty, CancellationToken.None);
                var lock3Result = await lock3.GetLock(TimeSpan.FromSeconds(1), string.Empty, CancellationToken.None);

                // Assert
                Assert.True(lock1Result);
                Assert.False(lock2Result);
                Assert.False(lock2Result);

                // Act
                lock1.Release();

                lock2Result = await lock2.GetLock(TimeSpan.FromSeconds(1), string.Empty, CancellationToken.None);
                lock3Result = await lock3.GetLock(TimeSpan.FromSeconds(1), string.Empty, CancellationToken.None);

                // Assert
                Assert.True(lock2Result);
                Assert.False(lock3Result);
            }
        }

        [Fact]
        public async Task FileSystemLock_DifferentFileSystemsAsync()
        {
            using (var target = new TestFolder())
            using (var cache1 = new LocalCache())
            using (var cache2 = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem1 = new PhysicalFileSystem(cache1, UriUtility.CreateUri(target.Root));
                var fileSystem2 = new PhysicalFileSystem(cache2, UriUtility.CreateUri(target.Root));

                var lock1 = fileSystem1.CreateLock(log);
                var lock2 = fileSystem2.CreateLock(log);

                // Act 1
                var lock1Result = await lock1.GetLock(TimeSpan.FromSeconds(1), string.Empty, CancellationToken.None);
                var lock2Result = await lock2.GetLock(TimeSpan.FromSeconds(1), string.Empty, CancellationToken.None);

                // Assert 1
                Assert.True(lock1Result);
                Assert.False(lock2Result);

                // Act 2
                lock1.Release();

                lock2Result = await lock2.GetLock(TimeSpan.FromSeconds(1), string.Empty, CancellationToken.None);

                // Assert 2
                Assert.True(lock2Result);
            }
        }

        [Fact]
        public async Task FileSystemLock_MultipleThreadsAsync()
        {
            using (var target = new TestFolder())
            {
                // Arrange
                var tasks = new List<Task<bool>>();
                var data = new ConcurrentDictionary<string, object>();

                // Act
                for (var i = 0; i < 100; i++)
                {
                    tasks.Add(Task.Run(async () => await ThreadWork(data, target.Root)));
                }

                // Assert
                foreach (var task in tasks)
                {
                    Assert.True(await task);
                }
            }
        }

        private static async Task<bool> ThreadWork(ConcurrentDictionary<string, object> data, string root)
        {
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(root));

                var lockData = fileSystem.CreateLock(log);

                var result = await lockData.GetLock(TimeSpan.FromMinutes(1), string.Empty, CancellationToken.None);

                try
                {
                    var obj = new object();
                    if (!data.TryAdd("test", obj)
                        || !data.TryRemove("test", out obj))
                    {
                        return false;
                    }
                }
                finally
                {
                    lockData.Release();
                }
            }

            return true;
        }
    }
}