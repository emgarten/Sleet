using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SecurityToken.Model;
using Amazon.SecurityToken;
using Amazon;
using Azure.Identity;
using Azure.Storage.Blobs;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using System.Net;
using NuGetUriUtility = NuGet.Common.UriUtility;

namespace Sleet
{

    public static class FileSystemFactory
    {
        /// <summary>
        /// Parses sleet.json to find the source and constructs it.
        /// </summary>
        public static async Task<ISleetFileSystem> CreateFileSystemAsync(LocalSettings settings, LocalCache cache, string source, ILogger log)
        {
            ISleetFileSystem result = null;

            var sources = settings.Json["sources"] as JArray;

            if (sources == null)
            {
                throw new ArgumentException("Invalid config. No sources found.");
            }

            foreach (var sourceEntry in sources.Select(e => (JObject)e))
            {
                var sourceName = JsonUtility.GetValueCaseInsensitive(sourceEntry, "name");

                if (source.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                {
                    var path = JsonUtility.GetValueCaseInsensitive(sourceEntry, "path");
                    var baseURIString = JsonUtility.GetValueCaseInsensitive(sourceEntry, "baseURI");
                    var feedSubPath = JsonUtility.GetValueCaseInsensitive(sourceEntry, "feedSubPath");
                    var type = JsonUtility.GetValueCaseInsensitive(sourceEntry, "type")?.ToLowerInvariant();

                    string absolutePath;
                    if (path != null && type == "local")
                    {
                        if (settings.Path == null && !Path.IsPathRooted(NuGetUriUtility.GetLocalPath(path)))
                        {
                            throw new ArgumentException("Cannot use a relative 'path' without a sleet.json file.");
                        }

                        var nonEmptyPath = path == "" ? "." : path;

                        var absoluteSettingsPath = NuGetUriUtility.GetAbsolutePath(Directory.GetCurrentDirectory(), settings.Path);

                        var settingsDir = Path.GetDirectoryName(absoluteSettingsPath);
                        absolutePath = NuGetUriUtility.GetAbsolutePath(settingsDir, nonEmptyPath);
                    }
                    else
                    {
                        absolutePath = path;
                    }

                    var pathUri = absolutePath != null ? UriUtility.EnsureTrailingSlash(UriUtility.CreateUri(absolutePath)) : null;
                    var baseUri = baseURIString != null ? UriUtility.EnsureTrailingSlash(UriUtility.CreateUri(baseURIString)) : pathUri;

                    if (type == "local")
                    {
                        if (pathUri == null)
                        {
                            throw new ArgumentException("Missing path for account.");
                        }

                        result = new PhysicalFileSystem(cache, pathUri, baseUri);
                    }
                    else if (type == "azure")
                    {
                        var connectionString = JsonUtility.GetValueCaseInsensitive(sourceEntry, "connectionString");
                        var container = JsonUtility.GetValueCaseInsensitive(sourceEntry, "container");

                        var blobServiceClient = await GetBlobServiceClient(log, connectionString, pathUri);

                        pathUri ??= blobServiceClient.Uri;
                        baseUri ??= pathUri;

                        result = new AzureFileSystem(cache, pathUri, baseUri, blobServiceClient, container, feedSubPath);
                    }
                    else if (type == "s3")
                    {
                        var profileName = JsonUtility.GetValueCaseInsensitive(sourceEntry, "profileName");
                        var accessKeyId = JsonUtility.GetValueCaseInsensitive(sourceEntry, "accessKeyId");
                        var secretAccessKey = JsonUtility.GetValueCaseInsensitive(sourceEntry, "secretAccessKey");
                        var bucketName = JsonUtility.GetValueCaseInsensitive(sourceEntry, "bucketName");
                        var region = JsonUtility.GetValueCaseInsensitive(sourceEntry, "region");
                        var serviceURL = JsonUtility.GetValueCaseInsensitive(sourceEntry, "serviceURL");
                        var serverSideEncryptionMethod = JsonUtility.GetValueCaseInsensitive(sourceEntry, "serverSideEncryptionMethod") ?? "None";
                        var compress = JsonUtility.GetBoolCaseInsensitive(sourceEntry, "compress", true);

                        if (string.IsNullOrEmpty(bucketName))
                        {
                            throw new ArgumentException("Missing bucketName for Amazon S3 account.");
                        }

                        if (string.IsNullOrEmpty(region) && string.IsNullOrEmpty(serviceURL))
                        {
                            throw new ArgumentException("Either 'region' or 'serviceURL' must be specified for an Amazon S3 account");
                        }
                        if (!string.IsNullOrEmpty(region) && !string.IsNullOrEmpty(serviceURL))
                        {
                            throw new ArgumentException("Options 'region' and 'serviceURL' cannot be used together");
                        }

                        if (serverSideEncryptionMethod != "None" && serverSideEncryptionMethod != "AES256")
                        {
                            throw new ArgumentException("Only 'None' or 'AES256' are currently supported for serverSideEncryptionMethod");
                        }

                        // Use the SDK value
                        var serverSideEncryptionMethodValue = ServerSideEncryptionMethod.None;
                        if (serverSideEncryptionMethod == "AES256")
                        {
                            serverSideEncryptionMethodValue = ServerSideEncryptionMethod.AES256;
                        }

                        var config = new AmazonS3Config()
                        {
                            Timeout = TimeSpan.FromSeconds(100),
                            ProxyCredentials = CredentialCache.DefaultNetworkCredentials
                        };


                        if (serviceURL != null)
                        {
                            config.ServiceURL = serviceURL;
                        }
                        else
                        {
                            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
                        }

                        AmazonS3Client amazonS3Client = null;

                        // Load credentials from the current profile
                        if (!string.IsNullOrWhiteSpace(profileName))
                        {
                            var credFile = new SharedCredentialsFile();
                            var chain = new CredentialProfileStoreChain();

                            if (credFile.TryGetProfile(profileName, out var profile))
                            {
                                // Successfully created the credentials using the profile
                                amazonS3Client = new AmazonS3Client(profile.GetAWSCredentials(profileSource: null), config);
                            }
                            else if (chain.TryGetAWSCredentials(profileName, out var credentials))
                            {
                                // Successfully created the credentials using a profile with SSO
                                // This works for identities outside of AWS such as Azure AD and Okta
                                amazonS3Client = new AmazonS3Client(credentials, config);
                            }
                            else
                            {
                                throw new ArgumentException($"The specified AWS profileName {profileName} could not be found. The feed must specify a valid profileName for an AWS credentials file. For help on credential files see: https://docs.aws.amazon.com/sdk-for-net/v2/developer-guide/net-dg-config-creds.html#creds-file");
                            }
                        }
                        // Load credentials explicitly with an accessKey and secretKey
                        else if (
                            !string.IsNullOrWhiteSpace(accessKeyId) &&
                            !string.IsNullOrWhiteSpace(secretAccessKey))
                        {
                            amazonS3Client = new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), config);
                        }
                        // Load credentials from Environment Variables
                        else if (
                            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariablesAWSCredentials.ENVIRONMENT_VARIABLE_ACCESSKEY)) &&
                            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariablesAWSCredentials.ENVIRONMENT_VARIABLE_SECRETKEY)))
                        {
                            amazonS3Client = new AmazonS3Client(new EnvironmentVariablesAWSCredentials(), config);
                        }
                        // Load credentials from an ECS docker container
                        else if (
                            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ECSTaskCredentials.ContainerCredentialsURIEnvVariable)))
                        {
                            amazonS3Client = new AmazonS3Client(new ECSTaskCredentials(), config);
                        }
                        // Assume IAM role
                        else
                        {
                            using (var client = new AmazonSecurityTokenServiceClient(config.RegionEndpoint))
                            {
                                try
                                {
                                    var identity = await client.GetCallerIdentityAsync(new GetCallerIdentityRequest());
                                }
                                catch (Exception ex)
                                {
                                    throw new ArgumentException(
                                        "Failed to determine AWS identity - ensure you have an IAM " +
                                        "role set, have set up default credentials or have specified a profile/key pair.", ex);
                                }
                            }

                            amazonS3Client = new AmazonS3Client(config);
                        }

                        if (pathUri == null)
                        {
                            // Find the default path
                            pathUri = AmazonS3Utility.GetBucketPath(bucketName, config.RegionEndpoint.SystemName);
                        }

                        if (baseUri == null)
                        {
                            baseUri = pathUri;
                        }

                        result = new AmazonS3FileSystem(
                            cache,
                            pathUri,
                            baseUri,
                            amazonS3Client,
                            bucketName,
                            serverSideEncryptionMethodValue,
                            feedSubPath,
                            compress);
                    }
                }
            }

            return result;
        }

        private static async Task<BlobServiceClient> GetBlobServiceClient(
            ILogger log,
            string connectionString,
            Uri pathUri)
        {
            if (pathUri is not null && connectionString is not null)
            {
                throw new ArgumentException("path (recommended) and connectionString (discouraged) are mutually exclusive for azure account. Chose one or the other.");
            }

            if (pathUri is not null)
            {
                return new BlobServiceClient(new Uri(pathUri.GetLeftPart(UriPartial.Authority)), new DefaultAzureCredential());
            }

            if (connectionString is null)
            {
                throw new ArgumentException("Missing path (recommended) or connectionString (discouraged) for azure account.");
            }

            await log.LogAsync(LogLevel.Warning,
                "connectionString (with access key) is not recommended for azure account. More information here: https://learn.microsoft.com/en-us/azure/storage/common/storage-account-keys-manage?tabs=azure-portal#protect-your-access-keys" + Environment.NewLine +
                "Use path instead.");


            if (connectionString.Equals(AzureFileSystem.AzureEmptyConnectionString, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid connectionString for azure account.");
            }

            return new BlobServiceClient(connectionString);
        }
    }
}
