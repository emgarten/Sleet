using FluentAssertions;
using NuGet.Versioning;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class UpgradeUtilityTests
    {
        [Fact]
        public void GivenAVersionVerifyAllowedRange()
        {
            UpgradeUtility.GetAllowedRange(new SemanticVersion(1, 2, 3), allowNewer: false)
                .ToNormalizedString()
                .Should()
                .Be("[1.2.0, 1.3.0)");
        }

        [Fact]
        public void GivenAVersionVerifyAllowedRangeHasNoUpperBound()
        {
            UpgradeUtility.GetAllowedRange(new SemanticVersion(1, 2, 3), allowNewer: true)
                .ToNormalizedString()
                .Should()
                .Be("[1.2.0, )");
        }
    }
}
