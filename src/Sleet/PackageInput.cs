using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class PackageInput
    {
        public DateTimeOffset Now { get; set; }

        public string PackagePath { get; set; }

        public ZipArchive Zip { get; set; }

        public PackageIdentity Identity { get; set; }

        public PackageArchiveReader Package { get; set; }

        // Thehse fields are populated by other steps
        public Uri NupkgUri { get; set; }

        public Uri PackageDetailsUri { get; set; }

        public Uri RegistrationUri { get; set; }
    }
}
