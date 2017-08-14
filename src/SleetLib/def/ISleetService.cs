namespace Sleet
{
    public interface ISleetService : IAddRemovePackages
    {
        /// <summary>
        /// Service name
        /// </summary>
        string Name { get; }
    }
}