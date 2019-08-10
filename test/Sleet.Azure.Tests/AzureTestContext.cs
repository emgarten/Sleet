using System;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using NuGet.Common;
using NuGet.Test.Helpers;

namespace Sleet.Azure.Tests
{
    public class AzureTestContext : IDisposable
    {
        public LocalSettings LocalSettings { get; }

        public AzureFileSystem FileSystem { get; set; }

        public string ContainerName { get; }

        public LocalCache LocalCache { get; }

        public CloudBlobContainer Container { get; }

        public Uri Uri => Container.Uri;

        public CloudStorageAccount StorageAccount { get; }

        public CloudBlobClient BlobClient { get; }

        public ILogger Logger { get; }

        public AzureTestContext()
        {
            ContainerName = Guid.NewGuid().ToString();
            StorageAccount = CloudStorageAccount.Parse(GetConnectionString());
            BlobClient = StorageAccount.CreateCloudBlobClient();
            Container = BlobClient.GetContainerReference(ContainerName);
            LocalCache = new LocalCache();
            LocalSettings = new LocalSettings();
            FileSystem = new AzureFileSystem(LocalCache, Uri, StorageAccount, ContainerName);
            Logger = new TestLogger();
        }

        public Task InitAsync()
        {
            return Container.CreateIfNotExistsAsync();
        }

        private bool _cleanupDone = false;
        public Task CleanupAsync()
        {
            _cleanupDone = true;
            return Container.DeleteIfExistsAsync();
        }

        public void Dispose()
        {
            LocalCache.Dispose();

            if (!_cleanupDone)
            {
                CleanupAsync().Wait();
            }
        }

        public const string EnvVarName = "SLEET_TEST_ACCOUNT";

        private static string GetConnectionString()
        {
            // Use a real azure storage account
            var s = Environment.GetEnvironmentVariable(EnvVarName);

            // Use this to run locally
            if (string.IsNullOrEmpty(s))
            {
                s = "UseDevelopmentStorage=true";
            }

            return s;
        }
    }
}
