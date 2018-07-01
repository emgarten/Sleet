using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage;

namespace Sleet
{
    public static class AzureUtility
    {
        public static Uri GetContainerPath(CloudStorageAccount azureAccount, string container)
        {
            var client = azureAccount.CreateCloudBlobClient();
            var blobContainer = client.GetContainerReference(container);
            return UriUtility.EnsureTrailingSlash(blobContainer.Uri);
        }

        public static Uri GetContainerAndSubFeedPath(CloudStorageAccount azureAccount, string container, string feedSubPath)
        {
            var uri = GetContainerPath(azureAccount, container);

            if (!string.IsNullOrEmpty(feedSubPath))
            {
                // Remove any slashes around the feed sub path and append it.
                uri = UriUtility.EnsureTrailingSlash(UriUtility.GetPath(uri, feedSubPath.Trim('/')));
            }

            return uri;
        }
    }
}
