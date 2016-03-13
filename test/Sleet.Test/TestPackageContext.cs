using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Sleet.Test
{
    public class TestPackageContext
    {
        public TestNuspecContext Nuspec { get; set; }

        public List<string> Files { get; set; } = new List<string>();

        public void Create(string outputDir)
        {
            throw new NotImplementedException();
        }
    }
}