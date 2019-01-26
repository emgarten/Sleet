using System.Threading;
using System.Threading.Tasks;

namespace Sleet
{
    public interface ISleetService : IApplyOperations
    {
        /// <summary>
        /// Service name
        /// </summary>
        string Name { get; }
    }
}