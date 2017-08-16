using System.Threading;
using System.Threading.Tasks;

namespace Sleet
{
    public interface ISleetService : IAddRemovePackages
    {
        /// <summary>
        /// Service name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Fetch the needed service files. This can be used to load
        /// all files in parallel.
        /// </summary>
        Task FetchAsync();
    }
}