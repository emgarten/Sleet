using Amazon.S3;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Used by services with no API for granting public read access, such as Cloudflare R2.
    /// Nothing is configured, instructions for enabling public access are logged instead.
    /// </summary>
    public class ExternalPublicAccessStrategy : IS3PublicAccessStrategy
    {
        private readonly S3Provider _provider;

        public ExternalPublicAccessStrategy(S3Provider provider)
        {
            _provider = provider;
        }

        public Task ConfigureBucketAsync(IAmazonS3 client, string bucketName, S3CannedACL? acl, ILogger log, CancellationToken token)
        {
            var message = _provider.GetPublicAccessHelpMessage?.Invoke(bucketName)
                ?? GetDefaultHelpMessage(bucketName);

            log.LogInformation(message);

            return Task.CompletedTask;
        }

        private string GetDefaultHelpMessage(string bucketName)
        {
            var message = $"{_provider.DisplayName} buckets are private by default and public access cannot be enabled through the S3 API."
                + Environment.NewLine
                + $"To allow NuGet clients to read this feed, enable public access on the '{bucketName}' bucket outside of Sleet.";

            if (!string.IsNullOrEmpty(_provider.HelpUrl))
            {
                message += Environment.NewLine + $"See: {_provider.HelpUrl}";
            }

            return message;
        }
    }
}
