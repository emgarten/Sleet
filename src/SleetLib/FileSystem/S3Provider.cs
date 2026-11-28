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
        /// Default for 'serviceURL'. Null requires serviceURL, except for Amazon S3 where region selects the endpoint.
        /// </summary>
        public string? ServiceUrl { get; init; }

        /// <summary>
        /// Default for 'region'. With a serviceURL it is only used to sign requests. Null leaves it to the AWS SDK.
        /// </summary>
        public string? Region { get; init; }

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
        /// Download gzip files as stored, by sending an unsigned 'Accept-Encoding: gzip' header.
        /// </summary>
        public bool AcceptGzip { get; init; }

        /// <summary>
        /// Public read access is enabled outside of the S3 API. Sleet does not configure access for new buckets.
        /// </summary>
        public bool ExternalPublicAccess { get; init; }

        /// <summary>
        /// The S3 API does not serve public reads, NuGet clients read the feed from baseURI.
        /// </summary>
        public bool RequiresBaseUri { get; init; }

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

            // R2 only accepts the region auto, and us-east-1 as an alias for it
            // https://developers.cloudflare.com/r2/api/s3/api/
            Region = "auto",

            // R2 does not support the streaming uploads or default checksums of the AWS SDK
            // https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/
            DisablePayloadSigning = true,
            ChecksumMode = RequestChecksumCalculation.WHEN_REQUIRED,

            // R2 does not implement bucket policies or ACLs, public buckets are served from a custom domain or r2.dev
            ExternalPublicAccess = true,
            RequiresBaseUri = true,
            HelpUrl = "https://developers.cloudflare.com/r2/buckets/public-buckets/"
        };

        public static readonly S3Provider SelfHosted = new()
        {
            Name = "self-hosted",
            DisplayName = "self-hosted S3",

            // Servers such as MinIO and RustFS only support virtual host style urls when they are configured with a domain
            ForcePathStyle = true
        };

        public static readonly S3Provider GoogleCloudStorage = new()
        {
            Name = "gcs",
            DisplayName = "Google Cloud Storage",

            // The XML API, which also serves public reads
            // https://cloud.google.com/storage/docs/aws-simple-migration
            ServiceUrl = "https://storage.googleapis.com",

            // GCS accepts any location in the signature, this avoids a region from AWS_REGION or the AWS config file
            // https://cloud.google.com/storage/docs/authentication/signatures
            Region = "auto",

            // Bucket names may contain dots, which do not match the TLS certificate of a virtual host style url
            ForcePathStyle = true,

            // GCS rejects the default checksums of the AWS SDK, and keeps aws-chunked in the Content-Encoding of signed uploads
            DisablePayloadSigning = true,
            ChecksumMode = RequestChecksumCalculation.WHEN_REQUIRED,

            // Without it GCS decompresses gzip files and returns a weak ETag, which fails the MD5 check of the AWS SDK
            // https://cloud.google.com/storage/docs/transcoding
            AcceptGzip = true,

            // Public read access is an IAM policy, which the XML API cannot set
            ExternalPublicAccess = true,
            HelpUrl = "https://cloud.google.com/storage/docs/access-control/making-data-public"
        };

        private static readonly S3Provider[] All = [Aws, CloudflareR2, SelfHosted, GoogleCloudStorage];

        /// <summary>
        /// Find a provider by name. Null or empty returns Amazon S3.
        /// </summary>
        public static S3Provider Get(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Aws;
            }

            var trimmed = name.Trim();

            return All.FirstOrDefault(e => e.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Unknown provider '{trimmed}' for s3 source. Valid values are: {string.Join(", ", All.Select(e => e.Name))}");
        }
    }
}
