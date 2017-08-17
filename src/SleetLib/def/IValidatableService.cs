using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// A service that validates itself.
    /// </summary>
    public interface IValidatableService
    {
        /// <summary>
        /// True if the internals of a service are valid.
        /// </summary>
        Task<IReadOnlyList<ILogMessage>> ValidateAsync();
    }
}
