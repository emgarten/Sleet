using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public static class SourceUtility
    {
        public static async Task VerifyInit(ISleetFileSystem fileSystem, ILogger log, CancellationToken token)
        {
            // Validate source
            var exists = await fileSystem.Validate(log, token);

            if (!exists)
            {
                throw new InvalidOperationException($"Unable to use feed.");
            }

            var indexPath = fileSystem.Get("index.json");

            if (!await indexPath.Exists(log, token))
            {
                throw new InvalidOperationException($"{fileSystem.BaseURI} is missing sleet files. Use 'sleet.exe init' to create a new feed.");
            }
        }
    }
}
