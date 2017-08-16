using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Sleet
{
    public class PackageInput : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public string PackagePath { get; set; }

        public ZipArchive Zip { get; set; }

        public PackageIdentity Identity { get; set; }

        public PackageArchiveReader Package { get; set; }

        // Thehse fields are populated by other steps
        public Uri NupkgUri { get; set; }

        public JObject PackageDetails { get; set; }

        public Uri RegistrationUri { get; set; }

        /// <summary>
        /// True if the package is a .symbols.nupkg
        /// </summary>
        public bool IsSymbolsPackage { get; set; }

        /// <summary>
        /// Run a non-thread safe action on the zip or package reader.
        /// </summary>
        public async Task<T> RunWithLockAsync<T>(Func<PackageInput, Task<T>> action)
        {
            await _semaphore.WaitAsync();

            try
            {
                return await action(this);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public override string ToString()
        {
            var s = $"{Identity.Id} {Identity.Version.ToFullVersionString()}";

            if (IsSymbolsPackage)
            {
                s += " (Symbols)";
            }

            return s;
        }

        public void Dispose()
        {
            Package?.Dispose();
            Package = null;

            Zip?.Dispose();
            Zip = null;

            _semaphore.Dispose();
        }
    }
}