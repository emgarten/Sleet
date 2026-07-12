using System;
using FluentAssertions;
using NuGet.Common;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class VerbosityUtilityTests
    {
        [Theory]
        [InlineData("quiet", LogLevel.Warning)]
        [InlineData("q", LogLevel.Warning)]
        [InlineData("minimal", LogLevel.Minimal)]
        [InlineData("m", LogLevel.Minimal)]
        [InlineData("normal", LogLevel.Information)]
        [InlineData("n", LogLevel.Information)]
        [InlineData("detailed", LogLevel.Verbose)]
        [InlineData("d", LogLevel.Verbose)]
        [InlineData("diagnostic", LogLevel.Debug)]
        [InlineData("diag", LogLevel.Debug)]
        public void VerbosityUtility_GetLogLevel_MapsKnownValues(string verbosity, LogLevel expected)
        {
            VerbosityUtility.GetLogLevel(verbosity).Should().Be(expected);
        }

        [Theory]
        [InlineData("Quiet")]
        [InlineData("MINIMAL")]
        [InlineData("  detailed  ")]
        public void VerbosityUtility_GetLogLevel_IsCaseInsensitiveAndTrimmed(string verbosity)
        {
            VerbosityUtility.TryGetLogLevel(verbosity, out _).Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData("loud")]
        [InlineData("verbose")]
        [InlineData(null)]
        public void VerbosityUtility_TryGetLogLevel_ReturnsFalseForUnknownValues(string verbosity)
        {
            VerbosityUtility.TryGetLogLevel(verbosity, out var level).Should().BeFalse();
            level.Should().Be(LogLevel.Information);
        }

        [Fact]
        public void VerbosityUtility_GetLogLevel_ThrowsForUnknownValue()
        {
            Action act = () => VerbosityUtility.GetLogLevel("loud");

            act.Should().Throw<ArgumentException>();
        }
    }
}
