using Azure.Storage.Blobs;
using NuGet.Common;
using NuGet.Test.Helpers;

namespace Sleet.Azure.Tests
{

    public class AzureTestContext : IAsyncDisposable
    {
        public LocalSettings LocalSettings { get; }

        public AzureFileSystem FileSystem { get; set; }

        public string ContainerName { get; }

        public LocalCache LocalCache { get; }

        public BlobContainerClient Container { get; }

        public Uri Uri => Container.Uri;

        public BlobServiceClient StorageAccount { get; }

        public ILogger Logger { get; }

        public bool CreateContainerOnInit = true;

        public AzureTestContext()
        {
            ContainerName = $"sleet-test-{Guid.NewGuid().ToString()}";
            StorageAccount = new BlobServiceClient(GetConnectionString());
            Container = StorageAccount.GetBlobContainerClient(ContainerName);
            LocalCache = new LocalCache();
            LocalSettings = new LocalSettings();
            FileSystem = new AzureFileSystem(LocalCache, Uri, StorageAccount, ContainerName);
            Logger = new TestLogger();
        }

        public async Task InitAsync()
        {
            if (CreateContainerOnInit)
            {
                await Container.CreateIfNotExistsAsync();
            }
        }

        private bool _cleanupDone = false;
        public async Task CleanupAsync()
        {
            _cleanupDone = true;
            await Container.DeleteIfExistsAsync();
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            LocalCache.Dispose();

            if (!_cleanupDone)
            {
                await CleanupAsync();
            }
        }

        public const string EnvVarName = "SLEET_TEST_ACCOUNT";

        public static string GetConnectionString()
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
