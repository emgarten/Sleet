namespace Sleet
{
    public interface IRootIndex
    {
        /// <summary>
        /// Root index file path.
        /// </summary>
        /// <remarks>Relative path</remarks>
        string RootIndex { get; }

        /// <summary>
        /// Returns the root index file.
        /// </summary>
        ISleetFile RootIndexFile { get; }
    }
}