using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class JsonUtilityTests
    {
        [Fact]
        public async Task JsonFilesShouldNotHaveABOM()
        {
            var json = new JObject();
            byte[] bytes = null;
            var expectedBytes = Encoding.UTF8.GetBytes(json.ToString());

            using (var stream = new MemoryStream())
            {
                await JsonUtility.WriteJsonAsync(json, stream);
                bytes = stream.ToArray();
            }

            bytes.SequenceEqual(expectedBytes).Should().BeTrue();
        }
    }
}
