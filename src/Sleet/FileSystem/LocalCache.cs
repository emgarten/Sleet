using System;
using System.IO;

namespace Sleet
{
    public class LocalCache : IDisposable
    {
        public LocalCache()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Root = new DirectoryInfo(path);
            Root.Create();
        }

        public DirectoryInfo Root { get; }

        public FileInfo GetNewTempPath()
        {
            return new FileInfo(Path.Combine(Root.FullName, Guid.NewGuid() + ".tmp"));
        }

        public void Dispose()
        {
            Root.Delete(recursive: true);
        }
    }
}