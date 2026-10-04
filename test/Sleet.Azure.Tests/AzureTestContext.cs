using Azure.Storage.Blobs;
using NuGet.Common;
using NuGet.Test.Helpers;
using Sleet.Test.Common;

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

        /// <summary>
        /// The host port of Azurite in local-env.
        /// </summary>
        public const int LocalEnvPort = 10100;

        /// <summary>
        /// The Azurite development account in local-env. UseDevelopmentStorage=true can't be used because it
        /// always connects to port 10000.
        /// </summary>
        public static readonly string LocalEnvConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:{LocalEnvPort}/devstoreaccount1;";

        /// <summary>
        /// True if SLEET_TEST_ACCOUNT is set or Azurite in local-env is running.
        /// </summary>
        public static bool IsAvailable => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarName)) || LocalEnvironment.IsListening(LocalEnvPort);

        public static string GetConnectionString()
        {
            // Use the account from the env var, such as a real Azure storage account
            var s = Environment.GetEnvironmentVariable(EnvVarName);

            // Otherwise use Azurite in local-env
            if (string.IsNullOrEmpty(s))
            {
                s = LocalEnvConnectionString;
            }

            return s;
        }
    }
}
