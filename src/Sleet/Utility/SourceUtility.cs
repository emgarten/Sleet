using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;

namespace Sleet
{
    public static class SourceUtility
    {
        public static async Task VerifyInit(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            var indexPath = fileSystem.Get("index.json");

            if (!await indexPath.Exists(log, token))
            {
                throw new InvalidOperationException($"{fileSystem.BaseURI} is missing sleet files. Use 'sleet.exe init' to create a new feed.");
            }
        }
    }
}
