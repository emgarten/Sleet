using System.Net;
using Amazon.S3;
using FluentAssertions;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class S3ProviderTests
    {
        [Fact]
        public void S3Provider_WithNoName_ReturnsAws()
        {
            S3Provider.Get(null).Should().BeSameAs(S3Provider.Aws);
            S3Provider.Get("").Should().BeSameAs(S3Provider.Aws);
            S3Provider.Get("  ").Should().BeSameAs(S3Provider.Aws);
        }

        [Theory]
        [InlineData("aws")]
        [InlineData("AWS")]
        [InlineData("amazon")]
        [InlineData("s3")]
        public void S3Provider_WithAwsNames_ReturnsAws(string name)
        {
            S3Provider.Get(name).Should().BeSameAs(S3Provider.Aws);
        }

        [Theory]
        [InlineData("r2")]
        [InlineData("R2")]
        [InlineData("cloudflare")]
        public void S3Provider_WithR2Names_ReturnsCloudflareR2(string name)
        {
            S3Provider.Get(name).Should().BeSameAs(S3Provider.CloudflareR2);
        }

        [Fact]
        public void S3Provider_WithUnknownName_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => S3Provider.Get("notarealservice"));
            ex.Message.Should().Contain("Unknown provider 'notarealservice'");
            ex.Message.Should().Contain("generic");
        }

        [Fact]
        public void S3Provider_AllHaveUniqueNames()
        {
            S3Provider.All.Select(e => e.Name).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void S3Provider_AllAreFoundByName()
        {
            foreach (var provider in S3Provider.All)
            {
                S3Provider.Get(provider.Name).Should().BeSameAs(provider);
            }
        }

        [Fact]
        public void S3Provider_AwsUsesDefaults()
        {
            // Changing any of these would change behavior for existing feeds.
            S3Provider.Aws.ForcePathStyle.Should().BeFalse();
            S3Provider.Aws.ChecksumMode.Should().Be(S3ChecksumMode.Default);
            S3Provider.Aws.DisablePayloadSigning.Should().BeFalse();
            S3Provider.Aws.PublicAccess.Should().Be(S3PublicAccessType.Aws);
            S3Provider.Aws.AuthenticationRegion.Should().BeNull();
            S3Provider.Aws.RequiresBaseUri.Should().BeFalse();
            S3Provider.Aws.AllowsAcl.Should().BeTrue();
            S3Provider.Aws.AllowsServerSideEncryption.Should().BeTrue();
        }

        [Fact]
        public void S3Provider_MinioUsesPathStyle()
        {
            // MinIO serves path style addressing unless wildcard DNS is configured.
            S3Provider.Minio.ForcePathStyle.Should().BeTrue();
            S3Provider.Minio.PublicAccess.Should().Be(S3PublicAccessType.BucketPolicy);
        }

        [Fact]
        public void S3Provider_YandexSkipsChecksums()
        {
            S3Provider.Yandex.ChecksumMode.Should().Be(S3ChecksumMode.WhenRequired);
            S3Provider.Yandex.AuthenticationRegion.Should().Be("ru-central1");
            S3Provider.Yandex.AllowsServerSideEncryption.Should().BeFalse();
        }

        [Fact]
        public void S3Provider_R2RequiresBaseUri()
        {
            S3Provider.CloudflareR2.RequiresBaseUri.Should().BeTrue();
            S3Provider.CloudflareR2.AllowsAcl.Should().BeFalse();
            S3Provider.CloudflareR2.AllowsServerSideEncryption.Should().BeFalse();
            S3Provider.CloudflareR2.AllowsRegion.Should().BeFalse();
            S3Provider.CloudflareR2.PublicAccess.Should().Be(S3PublicAccessType.External);
            S3Provider.CloudflareR2.ChecksumMode.Should().Be(S3ChecksumMode.WhenRequired);
            S3Provider.CloudflareR2.DisablePayloadSigning.Should().BeTrue();
        }

        [Theory]
        [InlineData(S3PublicAccessType.Aws, typeof(AwsPublicAccessStrategy))]
        [InlineData(S3PublicAccessType.BucketPolicy, typeof(BucketPolicyPublicAccessStrategy))]
        [InlineData(S3PublicAccessType.CannedAcl, typeof(CannedAclPublicAccessStrategy))]
        [InlineData(S3PublicAccessType.External, typeof(ExternalPublicAccessStrategy))]
        public void S3Provider_CreatePublicAccessStrategy_UsesOverride(S3PublicAccessType type, Type expected)
        {
            S3Provider.Generic.CreatePublicAccessStrategy(type).Should().BeOfType(expected);
        }

        [Fact]
        public void S3Provider_CreatePublicAccessStrategy_UsesProviderDefault()
        {
            S3Provider.Aws.CreatePublicAccessStrategy().Should().BeOfType<AwsPublicAccessStrategy>();
            S3Provider.Minio.CreatePublicAccessStrategy().Should().BeOfType<BucketPolicyPublicAccessStrategy>();
            S3Provider.CloudflareR2.CreatePublicAccessStrategy().Should().BeOfType<ExternalPublicAccessStrategy>();
            S3Provider.BackblazeB2.CreatePublicAccessStrategy().Should().BeOfType<CannedAclPublicAccessStrategy>();
        }

        [Fact]
        public void S3ErrorUtility_NotImplementedIsUnsupported()
        {
            // MinIO returns 501 for PutPublicAccessBlock and PutBucketOwnershipControls.
            var ex = new AmazonS3Exception("no") { StatusCode = HttpStatusCode.NotImplemented };

            S3ErrorUtility.IsUnsupportedOperation(ex).Should().BeTrue();
            S3ErrorUtility.CanRetry(ex).Should().BeFalse();
        }

        [Fact]
        public void S3ErrorUtility_MethodNotAllowedIsUnsupported()
        {
            var ex = new AmazonS3Exception("no") { StatusCode = HttpStatusCode.MethodNotAllowed };

            S3ErrorUtility.IsUnsupportedOperation(ex).Should().BeTrue();
            S3ErrorUtility.CanRetry(ex).Should().BeFalse();
        }

        [Theory]
        [InlineData("NotImplemented")]
        [InlineData("UnsupportedOperation")]
        [InlineData("MethodNotAllowed")]
        public void S3ErrorUtility_BadRequestErrorCodesAreUnsupported(string errorCode)
        {
            var ex = new AmazonS3Exception("no") { StatusCode = HttpStatusCode.BadRequest, ErrorCode = errorCode };

            S3ErrorUtility.IsUnsupportedOperation(ex).Should().BeTrue();
        }

        [Theory]
        [InlineData("MalformedPolicy")]
        [InlineData("InvalidRequest")]
        [InlineData("InvalidArgument")]
        [InlineData("AccessControlListNotSupported")]
        public void S3ErrorUtility_GenericBadRequestErrorCodesAreNotUnsupported(string errorCode)
        {
            // These are ordinary client errors. Treating them as "not implemented" would silently
            // skip a setting the user asked for.
            var ex = new AmazonS3Exception("no") { StatusCode = HttpStatusCode.BadRequest, ErrorCode = errorCode };

            S3ErrorUtility.IsUnsupportedOperation(ex).Should().BeFalse();
        }

        [Fact]
        public void S3ErrorUtility_ForbiddenIsNotUnsupported()
        {
            // 403 is a real permission error, and at least one service returns it for an
            // unrelated checksum incompatibility. Treating it as unsupported would hide both.
            var ex = new AmazonS3Exception("no") { StatusCode = HttpStatusCode.Forbidden, ErrorCode = "AccessDenied" };

            S3ErrorUtility.IsUnsupportedOperation(ex).Should().BeFalse();
            S3ErrorUtility.CanRetry(ex).Should().BeFalse();
        }

        [Fact]
        public void S3ErrorUtility_ServerErrorsCanRetry()
        {
            var ex = new AmazonS3Exception("no") { StatusCode = HttpStatusCode.InternalServerError };

            S3ErrorUtility.IsUnsupportedOperation(ex).Should().BeFalse();
            S3ErrorUtility.CanRetry(ex).Should().BeTrue();
        }
    }
}
