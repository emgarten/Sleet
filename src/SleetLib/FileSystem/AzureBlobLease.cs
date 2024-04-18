using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Azure.Storage;
using Azure.Storage.Blobs;

namespace Sleet
{
    using Azure.Storage.Blobs.Specialized;

    public class AzureBlobLease : IDisposable
    {
        private static readonly TimeSpan _leaseTime = new TimeSpan(0, 1, 0);
        private readonly BlobClient _blob;
        private readonly string _leaseId;

        public AzureBlobLease(BlobClient blob)
        {
            _blob = blob;
            _leaseId = Guid.NewGuid().ToString();
        }

        public string LeaseId => _leaseId;

        /// <summary>
        /// Makes a single attempt to get a lease.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> GetLease()
        {
            var actualLease = string.Empty;

            try
            {
                actualLease = (await _blob.GetBlobLeaseClient(_leaseId).AcquireAsync(_leaseTime)).Value.LeaseId;
            }
            catch (Exception ex)
            {
                Debug.Fail($"GetLease failed: {ex}");
            }

            return StringComparer.Ordinal.Equals(_leaseId, actualLease);
        }

        public async Task Renew()
        {
            try
            {
                await _blob.GetBlobLeaseClient(_leaseId).RenewAsync();
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

                _blob.GetBlobLeaseClient(_leaseId).ReleaseAsync().Wait(TimeSpan.FromSeconds(60));
            }
            catch (Exception ex)
            {
                // Ignore
                Debug.Fail($"Release failed: {ex}");
            }
        }
    }
}
