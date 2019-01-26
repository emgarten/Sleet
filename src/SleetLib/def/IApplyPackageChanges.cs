using System.Threading.Tasks;

namespace Sleet
{
    /// <summary>
    /// An alternative to IAddRemovePackages. All changes will be applied in a single call.
    /// </summary>
    public interface IApplyPackageChanges
    {
        /// <summary>
        /// Apply all adds and removes.
        /// </summary>
        Task ApplyChangesAsync(SleetChangeContext changeContext);
    }
}