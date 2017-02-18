using System;
using System.IO.Compression;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class PackageInput : IDisposable
    {
        public string PackagePath { get; set; }

        public ZipArchive Zip { get; set; }

        public PackageIdentity Identity { get; set; }

        public PackageArchiveReader Package { get; set; }

        // Thehse fields are populated by other steps
        public Uri NupkgUri { get; set; }

        public Uri PackageDetailsUri { get; set; }

        public Uri RegistrationUri { get; set; }

        public override string ToString()
        {
            return $"{Identity.Id} {Identity.Version.ToFullVersionString()}";
        }

        public void Dispose()
        {
            Package?.Dispose();
            Package = null;

            Zip?.Dispose();
            Zip = null;
        }
    }
}