using System.Net;
using Amazon.S3;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Shared handling for S3 errors across S3 compatible services.
    /// </summary>
    public static class S3ErrorUtility
    {
        private const HttpStatusCode NotImplemented = HttpStatusCode.NotImplemented;
        private const HttpStatusCode MethodNotAllowed = HttpStatusCode.MethodNotAllowed;

        /// <summary>
        /// Error codes returned with a 400 by services that do not implement an operation.
        /// </summary>
        private static readonly HashSet<string> UnsupportedErrorCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "NotImplemented",
            "InvalidRequest",
            "InvalidArgument",
            "MethodNotAllowed",
            "UnsupportedOperation"
        };

        /// <summary>
        /// True if the service does not implement the operation and it should be skipped.
        /// S3 compatible services implement different subsets of the S3 API, a bucket setting
        /// that cannot be applied is not a reason to fail the whole operation.
        /// </summary>
        /// <remarks>
        /// 403 is deliberately excluded. It is the real permission denied response, and at least
        /// one service returns 403 for an unrelated checksum incompatibility, so treating it as
        /// "not implemented" would hide genuine configuration problems.
        /// </remarks>
        public static bool IsUnsupportedOperation(AmazonS3Exception ex)
        {
            if (ex.StatusCode is NotImplemented or MethodNotAllowed)
            {
                return true;
            }

            return ex.StatusCode == HttpStatusCode.BadRequest
                && !string.IsNullOrEmpty(ex.ErrorCode)
                && UnsupportedErrorCodes.Contains(ex.ErrorCode);
        }

        /// <summary>
        /// True if the exception should be retried.
        /// </summary>
        public static bool CanRetry(AmazonS3Exception ex)
        {
            switch (ex.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.Unauthorized:

                // Services that do not implement an operation will keep returning the same
                // response, retrying only delays the failure.
                case NotImplemented:
                case MethodNotAllowed:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Retry S3 exceptions except for auth errors, bad requests, and unimplemented operations.
        /// </summary>
        public static async Task RetryAsync(Func<ILogger, CancellationToken, Task> func, S3Provider provider, ILogger log, CancellationToken token)
        {
            var start = DateTime.UtcNow;
            var maxTime = TimeSpan.FromMinutes(2);
            var failures = 0;

            while (true)
            {
                try
                {
                    await func(log, token);

                    // end
                    return;
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    // Forbidden indicates that the account info is correct, but the account lacks permissions, or the public access policy is blocking the change.
                    log.LogWarning(GetForbiddenHelpMessage(provider));
                    log.LogWarning($"Failed to update {provider.DisplayName} bucket. Status code: {ex.StatusCode} error: {ex.Message}");
                    throw;
                }
                catch (AmazonS3Exception ex) when (DateTime.UtcNow < start.Add(maxTime) && CanRetry(ex))
                {
                    // Only show the warning once.
                    // The last exception will throw.
                    if (failures == 0)
                    {
                        log.LogWarning($"Failed to update {provider.DisplayName} bucket. Trying again. Status code: {ex.StatusCode} error: {ex.Message}");
                    }

                    failures++;

                    // Ignore exceptions until the last exception
                    await Task.Delay(TimeSpan.FromMilliseconds(500), token);
                }
            }
        }

        /// <summary>
        /// Apply a bucket setting, skipping it when the service does not implement the operation.
        /// Returns false if the setting was skipped.
        /// </summary>
        public static async Task<bool> TryApplyAsync(
            Func<ILogger, CancellationToken, Task> func,
            string description,
            S3Provider provider,
            ILogger log,
            CancellationToken token)
        {
            try
            {
                await RetryAsync(func, provider, log, token);
                return true;
            }
            catch (AmazonS3Exception ex) when (IsUnsupportedOperation(ex))
            {
                await log.LogAsync(LogLevel.Verbose,
                    $"{provider.DisplayName} does not support {description}, skipping. Status code: {ex.StatusCode} error: {ex.Message}");

                return false;
            }
        }

        private static string GetForbiddenHelpMessage(S3Provider provider)
        {
            if (ReferenceEquals(provider, S3Provider.Aws))
            {
                // The user may not need AmazonS3FullAccess if they set a specific policy, but for diagnostics purposes it is the easiest way to help them rule out S3 access problems.
                return "Unable to update S3 bucket. Ensure that the login info used has AmazonS3FullAccess and that the AWS account allows public access buckets: https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/aws-properties-s3-bucket-publicaccessblockconfiguration.html";
            }

            var message = $"Unable to update {provider.DisplayName} bucket. Ensure that the credentials used are allowed to change the bucket configuration and that the service allows public buckets.";

            if (!string.IsNullOrEmpty(provider.HelpUrl))
            {
                message += $" See: {provider.HelpUrl}";
            }

            return message;
        }
    }
}
