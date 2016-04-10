using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Sleet.Test
{
    public class TestPackageContext
    {
        public TestPackageContext()
        {

        }

        public TestPackageContext(string id, string version)
        {
            Nuspec = new TestNuspecContext()
            {
                Id = id,
                Version = version
            };
        }

        public TestNuspecContext Nuspec { get; set; }

        public List<string> Files { get; set; } = new List<string>();

        public FileInfo Create(string outputDir)
        {
            var id = Nuspec.Id;
            var version = Nuspec.Version;

            var file = new FileInfo(Path.Combine(outputDir, $"{id}.{version}.nupkg"));

            file.Directory.Create();

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                foreach (var filePath in Files)
                {
                    var entry = zip.CreateEntry(filePath, CompressionLevel.Optimal);

                    using (var stream = entry.Open())
                    {
                        stream.Write(new byte[1] { 0 }, 0, 1);
                    }
                }

                var nuspecEntry = zip.CreateEntry($"{id}.nuspec", CompressionLevel.Optimal);

                using (var stream = nuspecEntry.Open())
                {
                    var xml = Nuspec.Create().ToString();

                    var xmlBytes = Encoding.UTF8.GetBytes(xml);

                    stream.Write(xmlBytes, 0, xmlBytes.Length);
                }
            }

            return file;
        }
    }
}