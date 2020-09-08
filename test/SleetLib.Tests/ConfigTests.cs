using System;
using System.IO;
using System.Linq;
using DotNetConfig;
using Newtonsoft.Json.Linq;
using Sleet;
using Xunit;

namespace SleetLib.Tests
{
    public class ConfigTests
    {
        [Fact]
        public void GivenItCanCanReadDotNetConfigVerifyJsonIsLoaded()
        {
            var configFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), Config.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(configFile));

            File.WriteAllText(configFile, @"
[sleet]
    feedLockTimeoutMinutes = 30
    username = foo
    useremail = foo@bar.com
    proxy-useDefaultCredentials = true

[sleet ""AzureFeed""]
    type = azure
    container = feed
    connectionString = ""DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=;BlobEndpoint=""
    path = https://myaccount.blob.core.windows.net/feed/
    feedSubPath = subPath

[sleet ""AmazonFeed""]
    type = s3
    bucketName = bucket
    region = us-east-1
    profileName = profile
    path = https://s3.amazonaws.com/my-bucket/
    feedSubPath = subPath
    serverSideEncryptionMethod = AES256
    compress

[sleet ""FolderFeed""]
    type = local
    path = C:\\feed
");

            var settings = LocalSettings.Load(configFile);
            Assert.NotNull(settings.Json);
            Assert.Equal(TimeSpan.FromMinutes(30), settings.FeedLockTimeout);
            Assert.True(settings.Json["proxy"]["useDefaultCredentials"].Value<bool>());

            // envvars are *not* applied because .netconfig contains sleet settings.
            Assert.Equal("foo", settings.Json["username"].ToString());
            Assert.Equal("foo@bar.com", settings.Json["useremail"].ToString());

            Assert.Equal(3, settings.Json["sources"].Children().Count());

            Assert.Equal("azure", settings.Json["sources"].First["type"].ToString());
            Assert.Equal("feed", settings.Json["sources"].First["container"].ToString());
            Assert.Equal("DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=;BlobEndpoint=", settings.Json["sources"].First["connectionString"].ToString());
            Assert.Equal("https://myaccount.blob.core.windows.net/feed/", settings.Json["sources"].First["path"].ToString());
            Assert.Equal("subPath", settings.Json["sources"].First["feedSubPath"].ToString());

            Assert.Equal("s3", settings.Json["sources"].Skip(1).First()["type"].ToString());
            Assert.Equal("bucket", settings.Json["sources"].Skip(1).First()["bucketName"].ToString());
            Assert.Equal("us-east-1", settings.Json["sources"].Skip(1).First()["region"].ToString());
            Assert.Equal("https://s3.amazonaws.com/my-bucket/", settings.Json["sources"].Skip(1).First()["path"].ToString());
            Assert.Equal("subPath", settings.Json["sources"].Skip(1).First()["feedSubPath"].ToString());
            Assert.Equal("AES256", settings.Json["sources"].Skip(1).First()["serverSideEncryptionMethod"].ToString());
            Assert.True(settings.Json["sources"].Skip(1).First()["compress"].Value<bool>());

            Assert.Equal("local", settings.Json["sources"].Skip(2).First()["type"].ToString());
            Assert.Equal("C:\\feed", settings.Json["sources"].Skip(2).First()["path"].ToString());
        }

        [Fact]
        public void GivenConfigSpecifiedWithNoSettingsThenThrows()
        {
            var file = Path.Combine(Path.GetTempPath(), Config.FileName);
            File.WriteAllText(file, @"
[config]
    editor = code
");


            Assert.Throws<ArgumentException>(() => LocalSettings.Load(file));
        }
    }
}
