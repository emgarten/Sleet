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
        public static async Task<ISleetFileSystem?> CreateFileSystemAsync(LocalSettings settings, LocalCache cache, string source, ILogger log)
        {
            ISleetFileSystem? result = null;

            var sources = settings.Json["sources"] as JArray;

            if (sources == null)
            {
                throw new ArgumentException("Invalid config. No sources found.");
            }

            foreach (var sourceEntry in sources.Select(e => (JObject)e))
            {
                var sourceName = JsonUtility.GetValueCaseInsensitive(sourceEntry, "name");

                if (source?.Equals(sourceName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    var path = JsonUtility.GetValueCaseInsensitive(sourceEntry, "path");
                    var baseURIString = JsonUtility.GetValueCaseInsensitive(sourceEntry, "baseURI");
                    var feedSubPath = JsonUtility.GetValueCaseInsensitive(sourceEntry, "feedSubPath");
                    var type = JsonUtility.GetValueCaseInsensitive(sourceEntry, "type")?.ToLowerInvariant();

                    string? absolutePath;
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

                    var pathUri = absolutePath != null ? UriUtility.EnsureTrailingSlash(UriUtility.CreateUri(absolutePath)) : (Uri?)null;
                    var baseUri = baseURIString != null ? UriUtility.EnsureTrailingSlash(UriUtility.CreateUri(baseURIString)) : pathUri;

                    if (type == "local")
                    {
                        if (pathUri == null)
                        {
                            throw new ArgumentException("Missing path for account.");
                        }

                        result = new PhysicalFileSystem(cache, pathUri, baseUri ?? pathUri);
                    }
                    else if (type == "azure")
                    {
                        var connectionString = JsonUtility.GetValueCaseInsensitive(sourceEntry, "connectionString");
                        var container = JsonUtility.GetValueCaseInsensitive(sourceEntry, "container");
                        var immutableCacheControlValue = JsonUtility.GetValueCaseInsensitive(sourceEntry, "immutableCacheControl");
                        var mutableCacheControlValue = JsonUtility.GetValueCaseInsensitive(sourceEntry, "mutableCacheControl");
                        var immutableCacheControl = string.IsNullOrWhiteSpace(immutableCacheControlValue) ? "no-store" : immutableCacheControlValue;
                        var mutableCacheControl = string.IsNullOrWhiteSpace(mutableCacheControlValue) ? "no-store" : mutableCacheControlValue;

                        if (string.IsNullOrEmpty(container))
                        {
                            throw new ArgumentException("Missing container for azure account.");
                        }

                        var blobServiceClient = await GetBlobServiceClient(log, connectionString, pathUri, container);

                        if (pathUri == null)
                        {
                            // Get the default url from the container
                            pathUri = AzureUtility.GetContainerPath(blobServiceClient, container);
                        }

                        baseUri ??= pathUri;

                        result = new AzureFileSystem(cache, pathUri, baseUri, blobServiceClient, container, feedSubPath, immutableCacheControl, mutableCacheControl);
                    }
                    else if (type == "s3")
                    {
                        var provider = S3Provider.Get(JsonUtility.GetValueCaseInsensitive(sourceEntry, "provider"));

                        var profileName = JsonUtility.GetValueCaseInsensitive(sourceEntry, "profileName");
                        var accessKeyId = JsonUtility.GetValueCaseInsensitive(sourceEntry, "accessKeyId");
                        var secretAccessKey = JsonUtility.GetValueCaseInsensitive(sourceEntry, "secretAccessKey");
                        var bucketName = JsonUtility.GetValueCaseInsensitive(sourceEntry, "bucketName");
                        var region = JsonUtility.GetValueCaseInsensitive(sourceEntry, "region");
                        var serviceURL = JsonUtility.GetValueCaseInsensitive(sourceEntry, "serviceURL");
                        var authenticationRegion = JsonUtility.GetValueCaseInsensitive(sourceEntry, "authenticationRegion");
                        var serverSideEncryptionMethod = JsonUtility.GetValueCaseInsensitive(sourceEntry, "serverSideEncryptionMethod") ?? "None";
                        var compress = JsonUtility.GetBoolCaseInsensitive(sourceEntry, "compress", true);
                        var acl = JsonUtility.GetValueCaseInsensitive(sourceEntry, "acl");
                        var disablePayloadSigning = JsonUtility.GetBoolCaseInsensitive(sourceEntry, "disablePayloadSigning", provider.DisablePayloadSigning);
                        var forcePathStyle = JsonUtility.GetBoolCaseInsensitive(sourceEntry, "forcePathStyle", provider.ForcePathStyle);
                        var checksumMode = GetChecksumMode(sourceEntry, provider);
                        var publicAccess = GetPublicAccessType(sourceEntry, provider);
                        var immutableCacheControl = JsonUtility.GetValueCaseInsensitive(sourceEntry, "immutableCacheControl");
                        if (string.IsNullOrWhiteSpace(immutableCacheControl))
                        {
                            immutableCacheControl = "no-store";
                        }
                        var mutableCacheControl = JsonUtility.GetValueCaseInsensitive(sourceEntry, "mutableCacheControl");
                        if (string.IsNullOrWhiteSpace(mutableCacheControl))
                        {
                            mutableCacheControl = "no-store";
                        }


                        if (string.IsNullOrEmpty(bucketName))
                        {
                            throw new ArgumentException($"Missing bucketName for {provider.DisplayName} account.");
                        }

                        // Providers that derive the endpoint from an account identifier build it here.
                        if (string.IsNullOrEmpty(serviceURL) && provider.ResolveServiceUrl != null)
                        {
                            serviceURL = provider.ResolveServiceUrl(sourceEntry);
                        }

                        if (string.IsNullOrEmpty(region) && string.IsNullOrEmpty(serviceURL))
                        {
                            var message = $"Either 'region' or 'serviceURL' must be specified for {provider.DisplayName}";

                            if (!string.IsNullOrEmpty(provider.ServiceUrlHint))
                            {
                                message += $". For {provider.DisplayName} serviceURL is usually {provider.ServiceUrlHint}";
                            }

                            throw new ArgumentException(message);
                        }

                        // 'region' alone only identifies an endpoint for Amazon S3. Without this
                        // a typo in serviceURL would silently send requests to Amazon.
                        if (string.IsNullOrEmpty(serviceURL) && !ReferenceEquals(provider, S3Provider.Aws))
                        {
                            var message = $"Missing serviceURL for {provider.DisplayName}. 'region' only selects an endpoint for Amazon S3.";

                            if (!string.IsNullOrEmpty(provider.ServiceUrlHint))
                            {
                                message += $" For {provider.DisplayName} serviceURL is usually {provider.ServiceUrlHint}";
                            }

                            throw new ArgumentException(message);
                        }

                        if (!string.IsNullOrEmpty(region) && !provider.AllowsRegion)
                        {
                            throw new ArgumentException($"Option 'region' is not supported by {provider.DisplayName}, which always signs requests with the '{provider.AuthenticationRegion}' region. Remove the setting, or set 'authenticationRegion' to override the signing region.");
                        }

                        if (serverSideEncryptionMethod != "None" && serverSideEncryptionMethod != "AES256")
                        {
                            throw new ArgumentException("Only 'None' or 'AES256' are currently supported for serverSideEncryptionMethod");
                        }

                        if (serverSideEncryptionMethod != "None" && !provider.AllowsServerSideEncryption)
                        {
                            throw new ArgumentException($"Option 'serverSideEncryptionMethod' is not supported by {provider.DisplayName}. Remove the setting or use a different provider.");
                        }

                        if (!string.IsNullOrEmpty(acl) && !provider.AllowsAcl)
                        {
                            var message = $"Option 'acl' is not supported by {provider.DisplayName}. Public read access must be configured outside of Sleet.";

                            if (!string.IsNullOrEmpty(provider.HelpUrl))
                            {
                                message += $" See: {provider.HelpUrl}";
                            }

                            throw new ArgumentException(message);
                        }

                        S3CannedACL? resolvedAcl = null;
                        if (acl != null)
                        {
                            resolvedAcl = S3CannedACL.FindValue(acl);
                        }
                        else if ((publicAccess ?? provider.PublicAccess) == S3PublicAccessType.CannedAcl)
                        {
                            // Access is granted by acl rather than a bucket policy, so uploaded
                            // objects need the same acl as the bucket to be readable.
                            resolvedAcl = S3CannedACL.PublicRead;
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
                            ProxyCredentials = CredentialCache.DefaultNetworkCredentials,
                            ForcePathStyle = forcePathStyle
                        };

                        if (checksumMode == S3ChecksumMode.WhenRequired)
                        {
                            // The SDK adds a CRC32 checksum to every request by default. Services that
                            // do not accept the x-amz-checksum headers reject those requests.
                            config.RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED;
                            config.ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED;
                        }

                        if (!string.IsNullOrEmpty(serviceURL))
                        {
                            config.ServiceURL = serviceURL;

                            // ServiceURL and RegionEndpoint are mutually exclusive on the config, so
                            // 'region' is used as the SigV4 signing region when a serviceURL is set.
                            var signingRegion = Coalesce(authenticationRegion, region, provider.AuthenticationRegion);

                            if (!string.IsNullOrEmpty(signingRegion))
                            {
                                config.AuthenticationRegion = signingRegion;
                            }
                        }
                        else
                        {
                            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);

                            if (!string.IsNullOrEmpty(authenticationRegion))
                            {
                                config.AuthenticationRegion = authenticationRegion;
                            }
                        }

                        AmazonS3Client? amazonS3Client = null;

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
                        // Check if the env var GenericContainerCredentials.RelativeURIEnvVariable exists
                        // Previously this used ECSTaskCredentials.RelativeURIEnvVariable but that was
                        // deprecated and the property is now internal on GenericContainerCredentials
                        else if (
                            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_CONTAINER_CREDENTIALS_RELATIVE_URI")))
                        {
                            amazonS3Client = new AmazonS3Client(new GenericContainerCredentials(), config);
                        }
                        // Assume IAM role
                        else
                        {
                            if (config.RegionEndpoint != null)
                            {
                                // STS is an Amazon service, it is only reachable on the region path.
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
                            }

                            try
                            {
                                // Falls back to the default credential chain, which covers instance
                                // profiles and a default credentials file profile.
                                amazonS3Client = new AmazonS3Client(config);
                            }
                            catch (Exception ex)
                            {
                                throw new ArgumentException(
                                    $"Missing accessKeyId and secretAccessKey for {provider.DisplayName} account, and no default credentials were found. "
                                    + "Set them in sleet.json, or set the AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables.", ex);
                            }
                        }

                        if (pathUri == null)
                        {
                            // Find the default path
                            pathUri = config.RegionEndpoint != null
                                ? AmazonS3Utility.GetBucketPath(bucketName, config.RegionEndpoint.SystemName)
                                : UriUtility.EnsureTrailingSlash(UriUtility.CreateUri($"{serviceURL!.TrimEnd('/')}/{bucketName}"));
                        }

                        // 'path' falling back to the bucket url does not satisfy providers that
                        // need a separate public url, so require baseURI to be set explicitly.
                        if (provider.RequiresBaseUri && baseURIString == null)
                        {
                            var message = $"Missing baseURI for {provider.DisplayName} account. Buckets are private by default and the public url cannot be determined automatically. "
                                + "Set baseURI to the public url of the bucket.";

                            if (!string.IsNullOrEmpty(provider.HelpUrl))
                            {
                                message += $" See: {provider.HelpUrl}";
                            }

                            throw new ArgumentException(message);
                        }

                        if (baseUri == null)
                        {
                            baseUri = pathUri;
                        }
                        else if (!string.IsNullOrEmpty(provider.PrivateEndpointHostSuffix)
                            && baseUri.Host.EndsWith(provider.PrivateEndpointHostSuffix, StringComparison.OrdinalIgnoreCase))
                        {
                            var message = $"baseURI is set to the {provider.DisplayName} API endpoint ({baseUri.AbsoluteUri}) which cannot be read by NuGet clients." + Environment.NewLine
                                + "Set baseURI to the public url of the bucket instead.";

                            if (!string.IsNullOrEmpty(provider.HelpUrl))
                            {
                                message += $" See: {provider.HelpUrl}";
                            }

                            await log.LogAsync(LogLevel.Warning, message);
                        }

                        result = new AmazonS3FileSystem(
                            cache,
                            pathUri,
                            baseUri,
                            amazonS3Client,
                            bucketName,
                            serverSideEncryptionMethodValue,
                            feedSubPath,
                            compress,
                            resolvedAcl,
                            disablePayloadSigning,
                            immutableCacheControl,
                            mutableCacheControl,
                            provider,
                            provider.CreatePublicAccessStrategy(publicAccess)
                        );
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Read 'checksumMode' from an s3 source, falling back to the provider default.
        /// </summary>
        private static S3ChecksumMode GetChecksumMode(JObject sourceEntry, S3Provider provider)
        {
            var value = JsonUtility.GetValueCaseInsensitive(sourceEntry, "checksumMode");

            if (string.IsNullOrWhiteSpace(value))
            {
                return provider.ChecksumMode;
            }

            if (Enum.TryParse<S3ChecksumMode>(value.Trim(), ignoreCase: true, out var result))
            {
                return result;
            }

            throw new ArgumentException($"Invalid checksumMode '{value}'. Valid values are: {string.Join(", ", Enum.GetNames<S3ChecksumMode>())}");
        }

        /// <summary>
        /// Read 'publicAccess' from an s3 source. Null uses the provider default.
        /// </summary>
        private static S3PublicAccessType? GetPublicAccessType(JObject sourceEntry, S3Provider provider)
        {
            var value = JsonUtility.GetValueCaseInsensitive(sourceEntry, "publicAccess");

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Enum.TryParse<S3PublicAccessType>(value.Trim(), ignoreCase: true, out var result))
            {
                return result;
            }

            throw new ArgumentException($"Invalid publicAccess '{value}' for {provider.DisplayName}. Valid values are: {string.Join(", ", Enum.GetNames<S3PublicAccessType>())}");
        }

        /// <summary>
        /// First value that is not null or empty.
        /// </summary>
        private static string? Coalesce(params string?[] values)
        {
            return values.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
        }

        private static async Task<BlobServiceClient> GetBlobServiceClient(            ILogger log,
            string? connectionString,
            Uri? pathUri,
            string? container)
        {
            // Path can be used with a connection string.
            // If the path is set and the connection string is not, use the default creds.
            if (pathUri is not null && connectionString is null)
            {
                return new BlobServiceClient(new Uri(pathUri.GetLeftPart(UriPartial.Authority)), new DefaultAzureCredential());
            }

            // Continue with connection string
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Missing connectionString for azure account.");
            }

            if (connectionString.Equals(AzureFileSystem.AzureEmptyConnectionString, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid connectionString for azure account.");
            }

            if (string.IsNullOrEmpty(container))
            {
                throw new ArgumentException("Missing container for azure account.");
            }

            await log.LogAsync(LogLevel.Verbose,
                "connectionString (with access key) is not recommended for azure account. More information here: https://learn.microsoft.com/en-us/azure/storage/common/storage-account-keys-manage?tabs=azure-portal#protect-your-access-keys" + Environment.NewLine +
                "Use path instead.");

            return new BlobServiceClient(connectionString);
        }
    }
}
