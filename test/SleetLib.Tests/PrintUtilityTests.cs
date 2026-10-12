using System;
using System.Globalization;
using AwesomeAssertions;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class PrintUtilityTests
    {
        [Theory]
        [InlineData(0, "0 KB")]
        [InlineData(1024, "1 KB")]
        [InlineData(1536, "1.5 KB")]
        [InlineData(1048577, "1 MB")]
        public void GetBytesString_FormatsKilobytesAndMegabytes(long bytes, string expected)
        {
            var originalCulture = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

                PrintUtility.GetBytesString(bytes).Should().Be(expected);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public void GetTimeString_WithShortDuration_CeilsMilliseconds()
        {
            PrintUtility.GetTimeString(TimeSpan.FromMilliseconds(1.2)).Should().Be("2 ms");
        }

        [Fact]
        public void GetTimeString_WithLongDuration_UsesTimeSpanString()
        {
            PrintUtility.GetTimeString(TimeSpan.FromMilliseconds(90001)).Should().Be(TimeSpan.FromMilliseconds(90001).ToString());
        }
    }
}
