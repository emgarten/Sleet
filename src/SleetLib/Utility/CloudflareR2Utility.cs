using System;
using System.Collections.Generic;

namespace Sleet
{
    /// <summary>
    /// Helpers for Cloudflare R2 feeds.
    /// </summary>
    public static class CloudflareR2Utility
    {
        /// <summary>
        /// R2 buckets always use the auto region for SigV4 signing.
        /// </summary>
        public const string Region = "auto";

        /// <summary>
        /// Jurisdiction used when none is specified.
        /// </summary>
        public const string DefaultJurisdiction = "default";

        /// <summary>
        /// Host suffix of the R2 S3 API endpoint. R2 does not serve public traffic from this host.
        /// </summary>
        public const string EndpointHostSuffix = ".r2.cloudflarestorage.com";

        /// <summary>
        /// Jurisdictions with a documented S3 API endpoint.
        /// </summary>
        private static readonly HashSet<string> Jurisdictions = new(StringComparer.OrdinalIgnoreCase)
        {
            DefaultJurisdiction,
            "eu",
            "us",
            "fedramp"
        };

        /// <summary>
        /// Build the S3 API endpoint for an R2 account.
        /// </summary>
        public static string GetServiceUrl(string? accountId, string? jurisdiction)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Missing accountId for Cloudflare R2 account. The account id can be found on the R2 overview page of the Cloudflare dashboard.");
            }

            if (!string.IsNullOrWhiteSpace(jurisdiction) && !Jurisdictions.Contains(jurisdiction))
            {
                throw new ArgumentException($"Invalid jurisdiction '{jurisdiction}' for Cloudflare R2 account. Valid values are: {string.Join(", ", Jurisdictions)}");
            }

            var host = string.IsNullOrWhiteSpace(jurisdiction)
                || StringComparer.OrdinalIgnoreCase.Equals(jurisdiction, DefaultJurisdiction)
                ? $"{accountId.Trim()}{EndpointHostSuffix}"
                : $"{accountId.Trim()}.{jurisdiction.Trim().ToLowerInvariant()}{EndpointHostSuffix}";

            return $"https://{host}";
        }

        /// <summary>
        /// Instructions shown after creating a bucket. R2 buckets are private by default and
        /// public access cannot be enabled through the S3 API.
        /// </summary>
        public static string GetPublicAccessHelpMessage(string bucketName)
        {
            return $"Cloudflare R2 buckets are private by default and public access cannot be enabled through the S3 API."
                + Environment.NewLine
                + $"To allow NuGet clients to read this feed, connect a custom domain to the '{bucketName}' bucket, or enable the managed r2.dev development URL."
                + Environment.NewLine
                + "See: https://developers.cloudflare.com/r2/buckets/public-buckets/";
        }
    }
}
