using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sleet
{
    public interface ISleetFileSystem
    {
        Uri Root { get; }

        ISleetFile Get(Uri path);

        ISleetFile Get(string relativePath);

        LocalCache LocalCache { get; }

        ConcurrentDictionary<Uri, ISleetFile> Files { get; }

        Uri GetPath(string relativePath);
    }
}
