using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Model;

namespace Sleet
{
    /// <summary>
    /// Downloads gzip files as stored from services that decompress them for clients that do not send 'Accept-Encoding: gzip'.
    /// </summary>
    internal sealed class AcceptGzipAmazonS3Client : AmazonS3Client
    {
        public AcceptGzipAmazonS3Client(AmazonS3Config config)
            : base(config)
        {
        }

        public AcceptGzipAmazonS3Client(AWSCredentials credentials, AmazonS3Config config)
            : base(credentials, config)
        {
        }

        protected override void CustomizeRuntimePipeline(RuntimePipeline pipeline)
        {
            base.CustomizeRuntimePipeline(pipeline);

            // Google Cloud Storage changes Accept-Encoding before it checks the signature, the header must not be signed
            pipeline.AddHandlerAfter<Signer>(new AcceptGzipHandler());
        }

        private sealed class AcceptGzipHandler : PipelineHandler
        {
            private const string AcceptEncoding = "Accept-Encoding";

            public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
            {
                var request = executionContext.RequestContext.Request;

                if (request.OriginalRequest is not GetObjectRequest)
                {
                    return await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);
                }

                request.Headers[AcceptEncoding] = "gzip";

                try
                {
                    return await base.InvokeAsync<T>(executionContext).ConfigureAwait(false);
                }
                finally
                {
                    // Clock skew and region retries sign the request again, the header must not be signed
                    request.Headers.Remove(AcceptEncoding);
                }
            }
        }
    }
}
