using System;
using System.IO;

namespace Sleet.Test
{
    public class TestFolder : IDisposable
    {
        public string Root
        {
            get
            {
                return Directory.FullName;
            }
        }

        public DirectoryInfo Directory { get; }

        public TestFolder()
        {
            Directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

            Directory.Create();
        }

        public void Dispose()
        {
            Directory.Delete(true);
        }
    }
}
