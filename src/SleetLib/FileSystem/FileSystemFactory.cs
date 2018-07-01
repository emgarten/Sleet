using System;
#if !SLEETLEGACY
using Amazon;
using Amazon.S3;
#endif
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;

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

            if (sources != null)
            {
                foreach (var sourceEntry in sources)
                {
                    if (source.Equals(sourceEntry["name"]?.ToObject<string>(), StringComparison.OrdinalIgnoreCase))
                    {
                        var path = sourceEntry["path"]?.ToObject<string>();
                        var baseURIString = sourceEntry["baseURI"]?.ToObject<string>();
                        var feedSubPath = sourceEntry["feedSubPath"]?.ToObject<string>();
                        var type = sourceEntry["type"]?.ToObject<string>().ToLowerInvariant();
                        var pathUri = path != null ? UriUtility.EnsureTrailingSlash(UriUtility.CreateUri(path)) : null;
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
                            var connectionString = sourceEntry["connectionString"]?.ToObject<string>();
                            var container = sourceEntry["container"]?.ToObject<string>();

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
                            var accessKeyId = sourceEntry["accessKeyId"]?.ToObject<string>();
                            var secretAccessKey = sourceEntry["secretAccessKey"]?.ToObject<string>();
                            var bucketName = sourceEntry["bucketName"]?.ToObject<string>();
                            var region = sourceEntry["region"]?.ToObject<string>();

                            if (string.IsNullOrEmpty(accessKeyId))
                                throw new ArgumentException("Missing accessKeyId for Amazon S3 account.");
                            if (string.IsNullOrEmpty(secretAccessKey))
                                throw new ArgumentException("Missing secretAccessKey for Amazon S3 account.");
                            if (string.IsNullOrEmpty(bucketName))
                                throw new ArgumentException("Missing bucketName for Amazon S3 account.");
                            if (string.IsNullOrEmpty(region))
                                throw new ArgumentException("Missing region for Amazon S3 account.");

                            var regionSystemName = RegionEndpoint.GetBySystemName(region);

                            if (pathUri == null)
                            {
                                // Find the default path
                                pathUri = AmazonS3Utility.GetBucketPath(bucketName, regionSystemName.SystemName);
                            }

                            if (baseUri == null)
                            {
                                baseUri = pathUri;
                            }

                            var amazonS3Client = new AmazonS3Client(
                                accessKeyId, secretAccessKey, regionSystemName);

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
            }

            return result;
        }
    }
}