using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Sleet
{
    /// <summary>
    /// Misc utils
    /// </summary>
    public static class Utility
    {
        /// <summary>
        /// Compress string
        /// </summary>
        public static async Task<MemoryStream> GZipAsync(string s)
        {
            var memoryStream = new MemoryStream();

            using (var zipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
            using (var writer = new StreamWriter(zipStream))
            {
                await writer.WriteAsync(s);
                await zipStream.FlushAsync();
                await memoryStream.FlushAsync();
            }

            memoryStream.Position = 0;

            return memoryStream;
        }

        /// <summary>
        /// Compress string
        /// </summary>
        public static async Task<MemoryStream> GZipAsync(Stream input)
        {
            var memoryStream = new MemoryStream();

            if (input.CanSeek)
            {
                input.Position = 0;
            }

            using (var zipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                await input.CopyToAsync(zipStream);
                await zipStream.FlushAsync();
                await memoryStream.FlushAsync();
            }

            memoryStream.Position = 0;

            return memoryStream;
        }
    }
}
