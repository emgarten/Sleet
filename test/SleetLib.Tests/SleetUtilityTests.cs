using FluentAssertions;
using NuGet.Test.Helpers;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class SleetUtilityTests
    {
        [Fact]
        public void SleetUtility_GetServiceName()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(target.Root));

                var file = fileSystem.Get("badges/v/test.json");
                var result = SleetUtility.GetServiceName(file);
                result.Should().Be(ServiceNames.Badges);

                file = fileSystem.Get("registration/a/a.json");
                result = SleetUtility.GetServiceName(file);
                result.Should().Be(ServiceNames.Registrations);

                file = fileSystem.Get("test.json");
                result = SleetUtility.GetServiceName(file);
                result.Should().Be(ServiceNames.Unknown);
            }
        }
        
    }
}
