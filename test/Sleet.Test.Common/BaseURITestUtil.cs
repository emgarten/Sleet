using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using Xunit;

namespace Sleet.Test.Common
{
    public static class BaseURITestUtil
    {
        public static async Task VerifyBaseUris(IEnumerable<string> filePaths, Uri baseUri)
        {
            var count = 0;

            foreach (var file in filePaths)
            {
                var fileJson = await JsonUtility.LoadJsonAsync(new FileInfo(file));

                foreach (var entityId in BaseURITestUtil.GetEntityIds(fileJson))
                {
                    count++;
                    Assert.True(entityId.StartsWith(baseUri.AbsoluteUri, StringComparison.Ordinal), $"{entityId} in {file}");
                }
            }

            Assert.True(count > 0, "No @id values were found to verify.");
        }

        public static async Task VerifyBaseUris(IEnumerable<ISleetFile> files, Uri baseUri)
        {
            var count = 0;

            foreach (var file in files)
            {
                if (file.RootPath.AbsoluteUri.EndsWith(".json", StringComparison.Ordinal))
                {
                    var fileJson = await file.GetJsonOrNull(NullLogger.Instance, CancellationToken.None);

                    if (fileJson != null)
                    {
                        foreach (var entityId in BaseURITestUtil.GetEntityIds(fileJson))
                        {
                            count++;
                            Assert.True(entityId.StartsWith(baseUri.AbsoluteUri, StringComparison.Ordinal), $"{entityId} in {fileJson}");
                        }
                    }
                }
            }

            Assert.True(count > 0, "No @id values were found to verify.");
        }

        /// <summary>
        /// Get all instances of @id outside of any @context
        /// </summary>
        public static IEnumerable<string> GetEntityIds(JToken json)
        {
            if (json is JObject jObj)
            {
                foreach (var prop in jObj.Properties())
                {
                    if (prop.Name == "@context")
                    {
                        continue;
                    }

                    if (prop.Name == "@id" && prop.Value.Type == JTokenType.String)
                    {
                        yield return prop.Value.ToObject<string>()!;
                    }
                    else
                    {
                        foreach (var id in GetEntityIds(prop.Value))
                        {
                            yield return id;
                        }
                    }
                }
            }
            else if (json is JArray jArray)
            {
                foreach (var item in jArray)
                {
                    foreach (var id in GetEntityIds(item))
                    {
                        yield return id;
                    }
                }
            }
        }
    }
}
