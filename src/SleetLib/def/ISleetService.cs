using System.Threading;
using System.Threading.Tasks;

namespace Sleet
{
    public interface ISleetService
    {
        /// <summary>
        /// Service name
        /// </summary>
        string Name { get; }
    }
}