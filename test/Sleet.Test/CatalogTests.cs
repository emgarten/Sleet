using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace Sleet.Test
{
    public class CatalogTests
    {
        [Fact]
        public void CatalogTest_CreatePackageDetails()
        {
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                // Arrange
                var log = new TestLogger();
                var fileSystem = new PhysicalFileSystem(cache, new Uri(target.Root));
                var settings = new LocalSettings();

                var context = new SleetContext()
                {
                    Token = CancellationToken.None,
                    LocalSettings = settings,
                    Log = log,
                    Source = fileSystem,
                    SourceSettings = new SourceSettings()
                };

                var catalog = new Catalog(context);

                var input = new PackageInput()
                {
                    Identity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0-alpha.1")),
                    NupkgUri = new Uri("http://tempuri.org/flatcontainer/packageA/1.0.0-alpha.1/packageA.1.0.0-alpha.1.nupkg"),
                    // TODO: add zip
                };

                // Act
                var actual = catalog.CreatePackageDetails(input);

                // Assert
                Assert.True(actual["@id"].ToString().EndsWith("/packagea.1.0.0-alpha.1.json"));
            }
        }
    }
}
