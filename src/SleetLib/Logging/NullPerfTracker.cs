using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public class NullPerfTracker : IPerfTracker
    {
        public void Add(PerfEntryBase entry)
        {
            // Noop
        }

        public Task LogSummary(ILogger log)
        {
            // Noop
            return Task.FromResult(true);
        }

        public static NullPerfTracker Instance = new NullPerfTracker();
    }
}
