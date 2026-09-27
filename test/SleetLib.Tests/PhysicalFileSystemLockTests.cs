using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    // Some tests change the working directory to verify that it is not used for the lock.
    [Collection(WorkingDirectoryCollection.Name)]
    public class PhysicalFileSystemLockTests
    {
        [Fact]
        public async Task PhysicalFileSystemLock_WaitingClientShowsHolderMessage()
        {
            using (var target = new TestFolder())
            {
                var waiterLog = new TestLogger();
                var lockPath = Path.Combine(target.Root, PhysicalFileSystemLock.LockFile);

                using (var holder = new PhysicalFileSystemLock(lockPath, new TestLogger()))
                using (var waiter = new PhysicalFileSystemLock(lockPath, waiterLog))
                {
                    (await holder.GetLock(TimeSpan.FromSeconds(1), "holder message", TestContext.Current.CancellationToken)).Should().BeTrue();
                    (await waiter.GetLock(TimeSpan.Zero, "waiter message", TestContext.Current.CancellationToken)).Should().BeFalse();

                    var messages = waiterLog.GetMessages();
                    messages.Should().Contain("Feed is locked by: holder message since: ");
                    messages.Should().NotContain("waiter message");
                    messages.Should().Contain($"Delete {PhysicalFileSystemLock.LockFile} to forcibly unlock the feed.");
                    messages.Should().Contain("Unable to obtain a lock on the feed.");
                    waiter.IsLocked.Should().BeFalse();
                }
            }
        }

        [Fact]
        public async Task PhysicalFileSystemLock_LockFileInWorkingDirectoryDoesNotBlockFeed()
        {
            using (var workingDir = new TestFolder())
            using (var target = new TestFolder())
            {
                var unrelatedLock = new JObject(
                    new JProperty("date", DateTime.UtcNow.ToString("o")),
                    new JProperty("message", "unrelated feed"));
                File.WriteAllText(Path.Combine(workingDir.Root, PhysicalFileSystemLock.LockFile), unrelatedLock.ToString());

                var originalDir = Directory.GetCurrentDirectory();

                try
                {
                    Directory.SetCurrentDirectory(workingDir.Root);

                    using (var feedLock = new PhysicalFileSystemLock(Path.Combine(target.Root, PhysicalFileSystemLock.LockFile), new TestLogger()))
                    {
                        (await feedLock.GetLock(TimeSpan.FromSeconds(5), "message", TestContext.Current.CancellationToken)).Should().BeTrue();
                    }
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDir);
                }
            }
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("")]
        [InlineData("not json")]
        public async Task PhysicalFileSystemLock_LockFileWithoutMessageIsNotReplaced(string content)
        {
            using (var target = new TestFolder())
            {
                var log = new TestLogger();
                var lockPath = Path.Combine(target.Root, PhysicalFileSystemLock.LockFile);
                File.WriteAllText(lockPath, content);

                using (var feedLock = new PhysicalFileSystemLock(lockPath, log))
                {
                    (await feedLock.GetLock(TimeSpan.Zero, "message", TestContext.Current.CancellationToken)).Should().BeFalse();
                }

                log.GetMessages().Should().Contain("Client holding the lock did not provide a message");
                File.ReadAllText(lockPath).Should().Be(content);
            }
        }

        [Fact]
        public async Task PhysicalFileSystemLock_DisposeReleasesLock()
        {
            using (var target = new TestFolder())
            {
                // The lock directory is created if needed.
                var lockPath = Path.Combine(target.Root, "feed", PhysicalFileSystemLock.LockFile);
                var feedLock = new PhysicalFileSystemLock(lockPath, new TestLogger());

                (await feedLock.GetLock(TimeSpan.FromSeconds(1), "lock message", TestContext.Current.CancellationToken)).Should().BeTrue();
                feedLock.IsLocked.Should().BeTrue();

                var json = JObject.Parse(File.ReadAllText(lockPath));
                json["message"].ToString().Should().Be("lock message");
                json["pid"].ToObject<int>().Should().Be(Environment.ProcessId);

                feedLock.Dispose();

                feedLock.IsLocked.Should().BeFalse();
                File.Exists(lockPath).Should().BeFalse();
            }
        }
    }
}
