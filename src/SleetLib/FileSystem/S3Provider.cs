using Amazon.Runtime;

namespace Sleet
{
    /// <summary>
    /// Defaults for an S3 compatible service, selected with the 'provider' property of an s3 source.
    /// Settings in sleet.json override these.
    /// </summary>
    internal sealed class S3Provider
    {
        /// <summary>
        /// Value of the 'provider' property.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Service name used in messages.
        /// </summary>
        public required string DisplayName { get; init; }

        /// <summary>
        /// Default for 'forcePathStyle'.
        /// </summary>
        public bool ForcePathStyle { get; init; }

        /// <summary>
        /// Default for 'disablePayloadSigning'.
        /// </summary>
        public bool DisablePayloadSigning { get; init; }

        /// <summary>
        /// Default for 'checksumMode'. Null uses the AWS SDK default.
        /// </summary>
        public RequestChecksumCalculation? ChecksumMode { get; init; }

        /// <summary>
        /// Public read access is enabled outside of the S3 API and served from a different url.
        /// Sleet does not configure access for new buckets, and baseURI is required.
        /// </summary>
        public bool ExternalPublicAccess { get; init; }

        /// <summary>
        /// Help for enabling public access.
        /// </summary>
        public string? HelpUrl { get; init; }

        public static readonly S3Provider Aws = new()
        {
            Name = "aws",
            DisplayName = "Amazon S3"
        };

        public static readonly S3Provider CloudflareR2 = new()
        {
            Name = "r2",
            DisplayName = "Cloudflare R2",

            // R2 does not support the streaming uploads or default checksums of the AWS SDK
            // https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/
            DisablePayloadSigning = true,
            ChecksumMode = RequestChecksumCalculation.WHEN_REQUIRED,

            // R2 does not implement bucket policies or ACLs
            ExternalPublicAccess = true,
            HelpUrl = "https://developers.cloudflare.com/r2/buckets/public-buckets/"
        };

        public static readonly S3Provider Minio = new()
        {
            Name = "minio",
            DisplayName = "MinIO",

            // MinIO only supports virtual host style urls when MINIO_DOMAIN is set
            ForcePathStyle = true
        };

        private static readonly S3Provider[] All = [Aws, CloudflareR2, Minio];

        /// <summary>
        /// Find a provider by name. Null or empty returns Amazon S3.
        /// </summary>
        public static S3Provider Get(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Aws;
            }

            return All.FirstOrDefault(e => e.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Unknown provider '{name}' for s3 source. Valid values are: {string.Join(", ", All.Select(e => e.Name))}");
        }
    }
}
