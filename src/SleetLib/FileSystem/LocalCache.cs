using System;
using System.IO;

namespace Sleet
{
    public class LocalCache : IDisposable
    {
        public LocalCache()
            : this(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
        {
        }

        public LocalCache(IPerfTracker perfTracker)
            : this(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), perfTracker)
        {
        }

        public LocalCache(string path)
            : this(path, perfTracker: null)
        {
        }

        public LocalCache(string path, IPerfTracker perfTracker)
        {
            Root = new DirectoryInfo(path);
            Root.Create();
            PerfTracker = perfTracker ?? NullPerfTracker.Instance;
        }

        public DirectoryInfo Root { get; }

        /// <summary>
        /// Performance related tracking
        /// </summary>
        public IPerfTracker PerfTracker { get; }

        public FileInfo GetNewTempPath()
        {
            return new FileInfo(Path.Combine(Root.FullName, Guid.NewGuid() + ".tmp"));
        }

        public void Dispose()
        {
            try
            {
                Root.Delete(recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}