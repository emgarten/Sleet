using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Sleet
{
    public class AzureBlobLease : IDisposable
    {
        private static readonly TimeSpan _leaseTime = new TimeSpan(0, 1, 0);
        private readonly CloudBlockBlob _blob;
        private readonly string _leaseId;

        public AzureBlobLease(CloudBlockBlob blob)
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
                actualLease = await _blob.AcquireLeaseAsync(_leaseTime, _leaseId);
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
                await _blob.RenewLeaseAsync(AccessCondition.GenerateLeaseCondition(_leaseId));
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
                _blob.ReleaseLeaseAsync(AccessCondition.GenerateLeaseCondition(_leaseId)).Wait(TimeSpan.FromSeconds(60));
            }
            catch (Exception ex)
            {
                // Ignore
                Debug.Fail($"Release failed: {ex}");
            }
        }
    }
}