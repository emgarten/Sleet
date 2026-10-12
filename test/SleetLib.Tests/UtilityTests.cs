using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class UtilityTests
    {
        [Fact]
        public async Task GZipAsync_WithString_RoundTripsContent()
        {
            using (var compressed = await Utility.GZipAsync("hello world"))
            using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip))
            {
                (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be("hello world");
            }
        }

        [Fact]
        public async Task GZipAsync_WithSeekableStream_ResetsInputAndRoundTripsContent()
        {
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes("abcdef")))
            {
                input.Position = 3;

                using (var compressed = await Utility.GZipAsync(input))
                using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip))
                {
                    (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be("abcdef");
                }
            }
        }
    }
}
