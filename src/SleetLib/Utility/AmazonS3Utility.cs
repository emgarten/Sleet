using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Sleet
{
    public static class AmazonS3Utility
    {
        public static Uri GetBucketPath(string bucketName, string region)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals("us-east-1", region))
            {
                return new Uri($"https://s3.amazonaws.com/{bucketName}/");
            }

            return new Uri($"https://s3-{region}.amazonaws.com/{bucketName}/");
        }

        public static void EnsureIAMRoleOrThrow() {
            using (var client = new HttpClient()) {
                client.Timeout = TimeSpan.FromSeconds(1);
                try {
                    var response = client.GetAsync("http://169.254.169.254/latest/meta-data/iam/info").Result;
                    if (response.IsSuccessStatusCode) {
                        var text = response.Content.ReadAsStringAsync().Result;
                        dynamic status = JsonConvert.DeserializeObject(text);
                        if (null == status || null == status.Code) {
                            throw new ArgumentException("Unable to parse AWS metadata's IAM role status information");
                        }
                        if (Convert.ToString(status.Code) != "Success") {
                            throw new ArgumentException("AWS metadata indicates IAM role has not been " +
                                "successfully applied");
                        }
                    } else {
                        throw new ArgumentException(String.Format("Query failed ({0})", response.StatusCode));
                    }
                } catch (ArgumentException) {
                    throw;
                } catch (Exception) {
                    throw new Exception("Error reaching AWS metadata server");
                } 
            }
        }
    }
}
