using Newtonsoft.Json.Linq;

namespace Sleet
{
    /// <summary>
    /// Controls when the AWS SDK adds a checksum to a request. The SDK adds a CRC32 checksum to
    /// every request by default, several S3 compatible services reject requests containing it.
    /// </summary>
    public enum S3ChecksumMode
    {
        /// <summary>
        /// Use the AWS SDK default and add a checksum whenever the operation supports one.
        /// </summary>
        Default,

        /// <summary>
        /// Only add a checksum when the operation requires one.
        /// </summary>
        WhenRequired
    }

    /// <summary>
    /// How public read access is granted to a newly created bucket.
    /// </summary>
    public enum S3PublicAccessType
    {
        /// <summary>
        /// Remove the public access blocks, set object ownership, set a read-only bucket policy,
        /// then apply the acl if one was given. Only Amazon S3 implements all of these.
        /// </summary>
        Aws,

        /// <summary>
        /// Set a read-only bucket policy. Used by services that implement PutBucketPolicy but
        /// not the public access block or object ownership APIs.
        /// </summary>
        BucketPolicy,

        /// <summary>
        /// Set a canned acl on the bucket. Used by services that implement PutBucketAcl only.
        /// </summary>
        CannedAcl,

        /// <summary>
        /// The service has no API for granting public read access, it must be enabled outside
        /// of Sleet. baseURI is required for these services.
        /// </summary>
        External
    }

    /// <summary>
    /// Default settings for an S3 compatible service, selected with the 'provider' property of an
    /// s3 source. Everything here is a default. Settings in sleet.json always win, so a service
    /// that does not have an entry here can still be used with provider 'generic' plus the
    /// individual settings it needs.
    /// </summary>
    public class S3Provider
    {
        /// <summary>
        /// Provider used when a source does not specify one.
        /// </summary>
        public const string DefaultProviderName = "aws";

        /// <summary>
        /// Value used for the 'provider' property.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Service name used in log and error messages.
        /// </summary>
        public required string DisplayName { get; init; }

        /// <summary>
        /// Use path style bucket addressing, https://host/bucket/key instead of
        /// https://bucket.host/key.
        /// </summary>
        public bool ForcePathStyle { get; init; }

        /// <summary>
        /// When request checksums are added.
        /// </summary>
        public S3ChecksumMode ChecksumMode { get; init; } = S3ChecksumMode.Default;

        /// <summary>
        /// Region used for SigV4 signing when the service is reached through a serviceURL.
        /// </summary>
        public string? AuthenticationRegion { get; init; }

        /// <summary>
        /// Skip the streaming SigV4 payload signature.
        /// </summary>
        public bool DisablePayloadSigning { get; init; }

        /// <summary>
        /// How public read access is granted when Sleet creates the bucket.
        /// </summary>
        public S3PublicAccessType PublicAccess { get; init; } = S3PublicAccessType.BucketPolicy;

        /// <summary>
        /// The public url of the feed cannot be determined from the bucket, baseURI must be set.
        /// </summary>
        public bool RequiresBaseUri { get; init; }

        /// <summary>
        /// The service honors the 'acl' property.
        /// </summary>
        public bool AllowsAcl { get; init; } = true;

        /// <summary>
        /// The service uses the 'region' property. Services with a fixed signing region reject
        /// it so that a region copied from an Amazon S3 config cannot break request signing.
        /// </summary>
        public bool AllowsRegion { get; init; } = true;

        /// <summary>
        /// The service honors the 'serverSideEncryptionMethod' property.
        /// </summary>
        public bool AllowsServerSideEncryption { get; init; } = true;

        /// <summary>
        /// Example serviceURL shown when one is missing.
        /// </summary>
        public string? ServiceUrlHint { get; init; }

        /// <summary>
        /// Documentation link added to errors and help messages.
        /// </summary>
        public string? HelpUrl { get; init; }

        /// <summary>
        /// Host suffix of the service API endpoint for services that do not serve public traffic
        /// from it. Used to warn when baseURI points at an address NuGet clients cannot read.
        /// </summary>
        public string? PrivateEndpointHostSuffix { get; init; }

        /// <summary>
        /// Builds the serviceURL from the source settings for services that derive it from an
        /// account identifier. Returns null when the source must specify serviceURL itself.
        /// </summary>
        public Func<JObject, string?>? ResolveServiceUrl { get; init; }

        /// <summary>
        /// Feed name used in the sleet.json template created by createconfig.
        /// </summary>
        public string TemplateFeedName { get; init; } = "myS3Feed";

        /// <summary>
        /// Properties added after bucketName in the sleet.json template created by createconfig.
        /// </summary>
        public IReadOnlyList<(string Key, string Value)> TemplateProperties { get; init; } =
        [
            ("serviceURL", ""),
            ("region", ""),
            ("accessKeyId", ""),
            ("secretAccessKey", "")
        ];

        /// <summary>
        /// Notes shown after createconfig writes a template for this provider.
        /// </summary>
        public IReadOnlyList<string> SetupNotes { get; init; } = [];

        /// <summary>
        /// Instructions logged after creating a bucket for services where public access must be
        /// enabled outside of Sleet.
        /// </summary>
        public Func<string, string>? GetPublicAccessHelpMessage { get; init; }

        public static readonly S3Provider Aws = new()
        {
            Name = DefaultProviderName,
            DisplayName = "Amazon S3",
            PublicAccess = S3PublicAccessType.Aws,
            TemplateFeedName = "myAmazonS3Feed",
            TemplateProperties =
            [
                ("region", "us-east-1"),
                ("profileName", "credentialsFileProfileName")
            ],
            SetupNotes =
            [
                "AWS credentials can be specified directly in sleet.json using accessKeyId and secretAccessKey instead of profileName. By default sleet.json is set to use a credentials file profile. To configure keys see: https://docs.aws.amazon.com/sdk-for-net/v2/developer-guide/net-dg-config-creds.html#creds-file"
            ]
        };

        public static readonly S3Provider CloudflareR2 = new()
        {
            Name = "r2",
            DisplayName = "Cloudflare R2",
            ChecksumMode = S3ChecksumMode.WhenRequired,
            AuthenticationRegion = CloudflareR2Utility.Region,

            // R2 does not implement the streaming SigV4 payload signing used by the AWS SDK.
            DisablePayloadSigning = true,
            PublicAccess = S3PublicAccessType.External,
            RequiresBaseUri = true,

            // R2 accepts acl headers but ignores them, and always encrypts objects at rest.
            AllowsAcl = false,
            AllowsServerSideEncryption = false,

            // R2 always signs with the auto region.
            AllowsRegion = false,
            HelpUrl = "https://developers.cloudflare.com/r2/buckets/public-buckets/",
            PrivateEndpointHostSuffix = CloudflareR2Utility.EndpointHostSuffix,
            ResolveServiceUrl = static source => CloudflareR2Utility.GetServiceUrl(
                JsonUtility.GetValueCaseInsensitive(source, "accountId"),
                JsonUtility.GetValueCaseInsensitive(source, "jurisdiction")),
            GetPublicAccessHelpMessage = CloudflareR2Utility.GetPublicAccessHelpMessage,
            TemplateFeedName = "myCloudflareR2Feed",
            TemplateProperties =
            [
                ("accountId", "cloudflareAccountId"),
                ("accessKeyId", ""),
                ("secretAccessKey", ""),
                ("baseURI", "https://nuget.example.com/")
            ],
            SetupNotes =
            [
                "Cloudflare R2 buckets are private by default. Set baseURI to the public url of the bucket, either a custom domain connected to the bucket or the managed r2.dev development url. For help see: https://developers.cloudflare.com/r2/buckets/public-buckets/",
                "Create an R2 API token to fill in accessKeyId and secretAccessKey, or leave them blank to use the AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY environment variables. For help see: https://developers.cloudflare.com/r2/api/tokens/"
            ]
        };

        public static readonly S3Provider Minio = new()
        {
            Name = "minio",
            DisplayName = "MinIO",

            // MinIO serves path style addressing unless MINIO_DOMAIN and wildcard DNS are set up.
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
            PublicAccess = S3PublicAccessType.BucketPolicy,
            ServiceUrlHint = "http://localhost:9000",
            HelpUrl = "https://min.io/docs/minio/linux/administration/identity-access-management/policy-based-access-control.html",
            TemplateFeedName = "myMinioFeed",
            TemplateProperties =
            [
                ("serviceURL", "http://localhost:9000"),
                ("region", "us-east-1"),
                ("forcePathStyle", "true"),
                ("accessKeyId", ""),
                ("secretAccessKey", "")
            ]
        };

        public static readonly S3Provider Yandex = new()
        {
            Name = "yandex",
            DisplayName = "Yandex Object Storage",

            // Yandex does not accept the x-amz-checksum headers the SDK adds by default.
            ChecksumMode = S3ChecksumMode.WhenRequired,
            AuthenticationRegion = "ru-central1",
            PublicAccess = S3PublicAccessType.BucketPolicy,

            // Yandex supports aws:kms only, not the AES256 value Sleet can set.
            AllowsServerSideEncryption = false,

            // Yandex documents this host for the AWS SDK for .NET.
            ServiceUrlHint = "https://s3.yandexcloud.net",
            HelpUrl = "https://yandex.cloud/en/docs/storage/tools/aws-sdk-net",
            TemplateFeedName = "myYandexFeed",
            TemplateProperties =
            [
                ("serviceURL", "https://s3.yandexcloud.net"),
                ("region", "ru-central1"),
                ("accessKeyId", ""),
                ("secretAccessKey", "")
            ]
        };

        public static readonly S3Provider Scaleway = new()
        {
            Name = "scaleway",
            DisplayName = "Scaleway Object Storage",
            PublicAccess = S3PublicAccessType.BucketPolicy,
            ServiceUrlHint = "https://s3.fr-par.scw.cloud",
            HelpUrl = "https://www.scaleway.com/en/docs/object-storage/api-cli/bucket-operations/",
            TemplateFeedName = "myScalewayFeed",
            TemplateProperties =
            [
                ("serviceURL", "https://s3.fr-par.scw.cloud"),
                ("region", "fr-par"),
                ("accessKeyId", ""),
                ("secretAccessKey", "")
            ]
        };

        public static readonly S3Provider Wasabi = new()
        {
            Name = "wasabi",
            DisplayName = "Wasabi",
            PublicAccess = S3PublicAccessType.BucketPolicy,
            ServiceUrlHint = "https://s3.wasabisys.com",
            HelpUrl = "https://docs.wasabi.com/docs/how-do-i-use-aws-sdk-for-net-with-wasabi",
            TemplateFeedName = "myWasabiFeed",
            TemplateProperties =
            [
                ("serviceURL", "https://s3.wasabisys.com"),
                ("region", "us-east-1"),
                ("accessKeyId", ""),
                ("secretAccessKey", "")
            ]
        };

        public static readonly S3Provider BackblazeB2 = new()
        {
            Name = "b2",
            DisplayName = "Backblaze B2",

            // B2 rejects requests carrying the checksum headers the SDK adds by default.
            ChecksumMode = S3ChecksumMode.WhenRequired,
            PublicAccess = S3PublicAccessType.CannedAcl,
            ServiceUrlHint = "https://s3.us-west-004.backblazeb2.com",
            HelpUrl = "https://www.backblaze.com/docs/cloud-storage-s3-compatible-api",
            TemplateFeedName = "myBackblazeFeed",
            TemplateProperties =
            [
                ("serviceURL", "https://s3.us-west-004.backblazeb2.com"),
                ("region", "us-west-004"),
                ("accessKeyId", ""),
                ("secretAccessKey", "")
            ]
        };

        public static readonly S3Provider DigitalOcean = new()
        {
            Name = "digitalocean",
            DisplayName = "DigitalOcean Spaces",
            ChecksumMode = S3ChecksumMode.WhenRequired,
            PublicAccess = S3PublicAccessType.CannedAcl,
            ServiceUrlHint = "https://nyc3.digitaloceanspaces.com",
            HelpUrl = "https://docs.digitalocean.com/products/spaces/",
            TemplateFeedName = "myDigitalOceanFeed",
            TemplateProperties =
            [
                ("serviceURL", "https://nyc3.digitaloceanspaces.com"),
                ("region", "nyc3"),
                ("accessKeyId", ""),
                ("secretAccessKey", "")
            ]
        };

        /// <summary>
        /// Neutral starting point for a service without an entry of its own.
        /// </summary>
        public static readonly S3Provider Generic = new()
        {
            Name = "generic",
            DisplayName = "S3 compatible storage",
            PublicAccess = S3PublicAccessType.BucketPolicy
        };

        /// <summary>
        /// All known providers.
        /// </summary>
        public static readonly IReadOnlyList<S3Provider> All =
        [
            Aws,
            CloudflareR2,
            Minio,
            Yandex,
            Scaleway,
            Wasabi,
            BackblazeB2,
            DigitalOcean,
            Generic
        ];

        /// <summary>
        /// Aliases accepted in addition to the provider name.
        /// </summary>
        private static readonly Dictionary<string, S3Provider> Lookup = CreateLookup();

        /// <summary>
        /// Build the strategy used to grant public read access to a new bucket.
        /// </summary>
        public IS3PublicAccessStrategy CreatePublicAccessStrategy(S3PublicAccessType? publicAccessOverride = null)
        {
            return (publicAccessOverride ?? PublicAccess) switch
            {
                S3PublicAccessType.Aws => new AwsPublicAccessStrategy(this),
                S3PublicAccessType.CannedAcl => new CannedAclPublicAccessStrategy(this),
                S3PublicAccessType.External => new ExternalPublicAccessStrategy(this),
                _ => new BucketPolicyPublicAccessStrategy(this)
            };
        }

        /// <summary>
        /// Find a provider by name. Throws for an unknown name.
        /// </summary>
        public static S3Provider Get(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Aws;
            }

            if (Lookup.TryGetValue(name.Trim(), out var provider))
            {
                return provider;
            }

            throw new ArgumentException(
                $"Unknown provider '{name}' for s3 source. Valid values are: {string.Join(", ", All.Select(e => e.Name))}. "
                + "Use 'generic' for a service that does not have an entry.");
        }

        private static Dictionary<string, S3Provider> CreateLookup()
        {
            var lookup = new Dictionary<string, S3Provider>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in All)
            {
                lookup.Add(provider.Name, provider);
            }

            lookup.Add("amazon", Aws);
            lookup.Add("s3", Aws);
            lookup.Add("cloudflare", CloudflareR2);
            lookup.Add("backblaze", BackblazeB2);
            lookup.Add("spaces", DigitalOcean);

            return lookup;
        }
    }
}
