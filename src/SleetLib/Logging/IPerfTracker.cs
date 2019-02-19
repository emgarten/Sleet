using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public interface IPerfTracker
    {
        /// <summary>
        /// Add a perf entry
        /// </summary>
        /// <param name="entry"></param>
        void Add(PerfEntryBase entry);

        /// <summary>
        /// Write out the perf summary to the logger.
        /// </summary>
        Task LogSummary(ILogger log);
    }
}
