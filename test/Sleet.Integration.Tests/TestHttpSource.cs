using NuGet.Protocol;

namespace Sleet.Integration.Test
{
    public class TestHttpSource : HttpSourceResource
    {
        public TestHttpSource(HttpSource httpSource)
            : base(httpSource)
        {
        }
    }
}