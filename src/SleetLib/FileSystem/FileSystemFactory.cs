using System;
using System.IO;
using System.Linq;
using System.Net;
#if !SLEETLEGACY
using Amazon;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
#endif
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using NuGetUriUtility = NuGet.Common.UriUtility;

namespace Sleet
{
    public static class FileSystemFactory
    {
        /// <summary>
        /// Parses sleet.json to find the source and constructs it.
        /// </summary>
        public static ISleetFileSystem CreateFileSystem(LocalSettings settings, LocalCache cache, string source)
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

                        var azureAccount = CloudStorageAccount.Parse(connectionString);

                        if (pathUri == null)
                        {
                            // Get the default url from the container
                            pathUri = AzureUtility.GetContainerPath(azureAccount, container);
                        }

                        if (baseUri == null)
                        {
                            baseUri = pathUri;
                        }

                        result = new AzureFileSystem(cache, pathUri, baseUri, azureAccount, container, feedSubPath);
                    }
#if !SLEETLEGACY
                    else if (type == "s3")
                    {
                        var accessKeyId = JsonUtility.GetValueCaseInsensitive(sourceEntry, "accessKeyId");
                        var secretAccessKey = JsonUtility.GetValueCaseInsensitive(sourceEntry, "secretAccessKey");
                        var profileName = JsonUtility.GetValueCaseInsensitive(sourceEntry, "profileName");
                        var bucketName = JsonUtility.GetValueCaseInsensitive(sourceEntry, "bucketName");
                        var region = JsonUtility.GetValueCaseInsensitive(sourceEntry, "region");

                        if (string.IsNullOrEmpty(bucketName))
                        {
                            throw new ArgumentException("Missing bucketName for Amazon S3 account.");
                        }

                        if (string.IsNullOrEmpty(region))
                        {
                            throw new ArgumentException("Missing region for Amazon S3 account.");
                        }

                        if (string.IsNullOrEmpty(profileName) && string.IsNullOrEmpty(accessKeyId))
                        {
                            throw new ArgumentException("Must provide a profileName or accessKeyId and secretAccessKey for Amazon S3 account.");
                        }

                        var regionSystemName = RegionEndpoint.GetBySystemName(region);

                        var config = new AmazonS3Config()
                        {
                            RegionEndpoint = regionSystemName,
                            ProxyCredentials = CredentialCache.DefaultNetworkCredentials
                        };

                        AmazonS3Client amazonS3Client = null;

                        if (string.IsNullOrEmpty(profileName))
                        {
                            // Access key in sleet.json
                            if (string.IsNullOrEmpty(accessKeyId))
                            {
                                throw new ArgumentException("Missing accessKeyId for Amazon S3 account.");
                            }

                            if (string.IsNullOrEmpty(secretAccessKey))
                            {
                                throw new ArgumentException("Missing secretAccessKey for Amazon S3 account.");
                            }

                            amazonS3Client = new AmazonS3Client(accessKeyId, secretAccessKey, config);
                        }
                        else
                        {
                            // Avoid mismatched configs, this would get confusing for users.
                            if (!string.IsNullOrEmpty(accessKeyId) || !string.IsNullOrEmpty(secretAccessKey))
                            {
                                throw new ArgumentException("accessKeyId/secretAccessKey may not be used with profileName. Either use profileName with a credential file containing the access keys, or set the access keys in sleet.json and remove profileName.");
                            }

                            // Credential file
                            var credFile = new SharedCredentialsFile();
                            if (credFile.TryGetProfile(profileName, out var profile))
                            {
                                amazonS3Client = new AmazonS3Client(profile.GetAWSCredentials(profileSource: null), config);
                            }
                            else
                            {
                                throw new ArgumentException($"The specified AWS profileName {profileName} could not be found. The feed must specify a valid profileName for an AWS credentials file, or accessKeyId and secretAccessKey must be provided. For help on credential files see: https://docs.aws.amazon.com/sdk-for-net/v2/developer-guide/net-dg-config-creds.html#creds-file");
                            }
                        }

                        if (pathUri == null)
                        {
                            // Find the default path
                            pathUri = AmazonS3Utility.GetBucketPath(bucketName, regionSystemName.SystemName);
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
                            feedSubPath);
                    }
#endif
                }
            }

            return result;
        }
    }
}
