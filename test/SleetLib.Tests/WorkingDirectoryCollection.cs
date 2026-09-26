using Xunit;

namespace SleetLib.Tests
{
    /// <summary>
    /// Tests that change or read the process working directory run in this collection
    /// so they never run in parallel with other tests.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class WorkingDirectoryCollection
    {
        public const string Name = "Working directory";
    }
}
