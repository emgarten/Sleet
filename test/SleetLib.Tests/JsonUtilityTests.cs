using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class JsonUtilityTests
    {
        [Fact]
        public async Task JsonFilesShouldNotHaveABOM()
        {
            var json = new JObject();
            byte[] bytes = null;
            var expectedBytes = Encoding.UTF8.GetBytes(json.ToString());

            using (var stream = new MemoryStream())
            {
                await JsonUtility.WriteJsonAsync(json, stream);
                bytes = stream.ToArray();
            }

            bytes.SequenceEqual(expectedBytes).Should().BeTrue();
        }

        [Fact]
        public async Task GZipAndMinifyAsync_MinifiesAndCompressesJsonFromBeginningOfSeekableStream()
        {
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes("{\r\n  \"name\" : \"PackageA\"\r\n}")))
            {
                input.Position = input.Length;

                using (var compressed = await JsonUtility.GZipAndMinifyAsync(input))
                using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip))
                {
                    (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be("{\"name\":\"PackageA\"}");
                }
            }
        }

        [Fact]
        public async Task IsJsonAsync_ReturnsTrueForObjectJsonAndFalseForInvalidOrNonObjectJson()
        {
            using (var target = new TestFolder())
            {
                var valid = Path.Combine(target.Root, "valid.json");
                var invalid = Path.Combine(target.Root, "invalid.json");
                var array = Path.Combine(target.Root, "array.json");
                File.WriteAllText(valid, "{\"name\":\"PackageA\"}");
                File.WriteAllText(invalid, "not json");
                File.WriteAllText(array, "[]");

                (await JsonUtility.IsJsonAsync(valid)).Should().BeTrue();
                (await JsonUtility.IsJsonAsync(invalid)).Should().BeFalse();
                (await JsonUtility.IsJsonAsync(array)).Should().BeFalse();
            }
        }
    }
}
