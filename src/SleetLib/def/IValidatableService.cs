using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Validate internal parts of a service beyond the list of package identities.
    /// </summary>
    public interface IValidatableService
    {
        /// <summary>
        /// True if the internals of a service are valid.
        /// </summary>
        /// <remarks>This does not need to include package identities which the validate command already verifies.</remarks>
        Task<IReadOnlyList<ILogMessage>> ValidateAsync();
    }
}
