using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs;
using System.Diagnostics;

namespace Sleet
{
    public class AzureBlobLease : IDisposable
    {
        private static readonly TimeSpan _leaseTime = new TimeSpan(0, 1, 0);
        private readonly BlobClient _blob;

        public AzureBlobLease(BlobClient blob)
        {
            _blob = blob;
            LeaseId = Guid.NewGuid().ToString();
        }

        public string LeaseId { get; }

        /// <summary>
        /// Makes a single attempt to get a lease.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> GetLease()
        {
            var actualLease = string.Empty;

            try
            {
                actualLease = (await _blob.GetBlobLeaseClient(LeaseId).AcquireAsync(_leaseTime)).Value.LeaseId;
            }
            catch (Exception ex)
            {
                Debug.Fail($"GetLease failed: {ex}");
            }

            return StringComparer.Ordinal.Equals(LeaseId, actualLease);
        }

        public async Task Renew()
        {
            try
            {
                await _blob.GetBlobLeaseClient(LeaseId).RenewAsync();
            }
            catch (Exception ex)
            {
                // attempt to get the lease again
                await GetLease();
                Debug.Fail($"Renew failed: {ex}");
            }
        }

        public void Dispose()
        {
            Release();
        }

        public void Release()
        {
            try
            {

                _blob.GetBlobLeaseClient(LeaseId).ReleaseAsync().Wait(TimeSpan.FromSeconds(60));
            }
            catch (Exception ex)
            {
                // Ignore
                Debug.Fail($"Release failed: {ex}");
            }
        }
    }
}
