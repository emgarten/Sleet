using Xunit;

namespace SleetLib.Tests
{
    /// <summary>
    /// Tests that use the process working directory must not run in parallel.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class WorkingDirectoryCollection
    {
        public const string Name = "Working directory";
    }
}
