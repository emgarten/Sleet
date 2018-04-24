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
            foreach (var file in filePaths)
            {
                var fileJson = await JsonUtility.LoadJsonAsync(new FileInfo(file));

                foreach (var entityId in BaseURITestUtil.GetEntityIds(fileJson))
                {
                    Assert.True(entityId.StartsWith(baseUri.AbsoluteUri), $"{entityId} in {file}");
                }
            }
        }

        public static async Task VerifyBaseUris(IEnumerable<ISleetFile> files, Uri baseUri)
        {
            foreach (var file in files)
            {
                if (file.RootPath.AbsoluteUri.EndsWith(".json"))
                {
                    var fileJson = await file.GetJsonOrNull(NullLogger.Instance, CancellationToken.None);

                    if (fileJson != null)
                    {
                        foreach (var entityId in BaseURITestUtil.GetEntityIds(fileJson))
                        {
                            Assert.True(entityId.StartsWith(baseUri.AbsoluteUri), $"{entityId} in {fileJson}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get all instance of @id outside of the context
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetEntityIds(JObject json)
        {
            foreach (var node in json.Children())
            {
                if (node.Type == JTokenType.Property)
                {
                    var prop = (JProperty)node;

                    if (prop.Name != "@context")
                    {
                        if (prop.Value is JObject jObj)
                        {
                            foreach (var desc in jObj.DescendantsAndSelf())
                            {
                                var descProp = (JProperty)node;

                                if (descProp.Name == "@id")
                                {
                                    yield return descProp.Value.ToObject<string>();
                                }
                            }
                        }
                    }
                }
            }

            yield break;
        }
    }
}
