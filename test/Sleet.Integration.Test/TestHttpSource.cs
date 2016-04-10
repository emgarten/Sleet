using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

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
