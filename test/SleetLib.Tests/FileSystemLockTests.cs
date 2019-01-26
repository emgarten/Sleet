using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class FileSystemLockTests
    {
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
                var lock1Result = await lock1.GetLock(TimeSpan.FromSeconds(1), CancellationToken.None);
                var lock2Result = await lock2.GetLock(TimeSpan.FromSeconds(1), CancellationToken.None);
                var lock3Result = await lock3.GetLock(TimeSpan.FromSeconds(1), CancellationToken.None);

                // Assert
                Assert.True(lock1Result);
                Assert.False(lock2Result);
                Assert.False(lock2Result);

                // Act
                lock1.Release();

                lock2Result = await lock2.GetLock(TimeSpan.FromSeconds(1), CancellationToken.None);
                lock3Result = await lock3.GetLock(TimeSpan.FromSeconds(1), CancellationToken.None);

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
                var lock1Result = await lock1.GetLock(TimeSpan.FromSeconds(1), CancellationToken.None);
                var lock2Result = await lock2.GetLock(TimeSpan.FromSeconds(1), CancellationToken.None);

                // Assert 1
                Assert.True(lock1Result);
                Assert.False(lock2Result);

                // Act 2
                lock1.Release();

                lock2Result = await lock2.GetLock(TimeSpan.FromSeconds(1), CancellationToken.None);

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

                var result = await lockData.GetLock(TimeSpan.FromMinutes(1), CancellationToken.None);

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