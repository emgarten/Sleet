using Xunit;

namespace Sleet.Tests
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class NonParallelCollection
    {
        public const string Name = "Non-parallel";
    }
}
