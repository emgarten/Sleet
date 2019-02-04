using System.Threading.Tasks;

namespace Sleet
{
    /// <summary>
    /// An alternative to IAddRemovePackages. All changes will be applied in a single call.
    /// </summary>
    public interface IApplyOperations
    {
        /// <summary>
        /// Apply all adds and removes.
        /// </summary>
        Task ApplyOperationsAsync(SleetOperations operations);

        /// <summary>
        /// This provides an optional hook to fetch and load files in advance.
        /// </summary>
        Task PreLoadAsync(SleetOperations operations);
    }
}