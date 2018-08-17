using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class SourceUtilityTests
    {
        [Fact]
        public async Task GivenThatABaseUriChangesVerifyValidationFails()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var settings = new LocalSettings();


                var fileSystem1 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root), UriUtility.CreateUri("https://tempuri.org/"));
                var fileSystem2 = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root), UriUtility.CreateUri("https://tempuri.org/b/"));

                await InitCommand.RunAsync(settings, fileSystem1, log);

                await SourceUtility.EnsureBaseUriMatchesFeed(fileSystem1, log, CancellationToken.None);
                InvalidDataException foundEx = null;

                try
                {
                    await SourceUtility.EnsureBaseUriMatchesFeed(fileSystem2, log, CancellationToken.None);
                }
                catch (InvalidDataException ex)
                {
                    foundEx = ex;
                }

                foundEx.Message.Should().Contain("https://tempuri.org/");
            }
        }
    }
}
