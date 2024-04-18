using System;
using Azure.Storage;
using Azure.Storage.Blobs;

namespace Sleet
{
    public static class AzureUtility
    {
        public static Uri GetContainerPath(BlobServiceClient blobServiceClient, string container)
        {
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(container);
            return UriUtility.EnsureTrailingSlash(blobContainerClient.Uri);
        }
    }
}
