using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Sleet.Integration.Test
{
    /// <summary>
    /// HttpSource -> PhysicalFileSystem adapter
    /// </summary>
    public class TestHttpSourceResourceProvider : ResourceProvider
    {
        private readonly ConcurrentDictionary<PackageSource, HttpSourceResource> _cache
                = new ConcurrentDictionary<PackageSource, HttpSourceResource>();

        private readonly PhysicalFileSystem _fileSystem;

        public TestHttpSourceResourceProvider(PhysicalFileSystem fileSystem)
            : base(typeof(HttpSourceResource),
                  nameof(TestHttpSourceResourceProvider),
                  nameof(HttpSourceResource))
        {
            _fileSystem = fileSystem;
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            HttpSourceResource curResource = null;

            if (source.PackageSource.IsHttp)
            {
                curResource = _cache.GetOrAdd(source.PackageSource,
                    (packageSource) => new HttpSourceResource(CreateSource(source, _fileSystem)));
            }

            return Task.FromResult(new Tuple<bool, INuGetResource>(curResource != null, curResource));
        }

        private static HttpSource CreateSource(SourceRepository source, PhysicalFileSystem fileSystem)
        {
            Func<Task<HttpHandlerResource>> handlerFactory = () =>
            {
                return Task.FromResult<HttpHandlerResource>(new TestHttpHandlerResource(fileSystem));
            };

            return new TestHttpSource(source.PackageSource, handlerFactory, fileSystem);
        }

        private class TestHttpHandlerResource : HttpHandlerResource
        {
            private readonly PhysicalFileSystem _fileSystem;
            private readonly HttpMessageHandler _messageHandler;

            public TestHttpHandlerResource(PhysicalFileSystem fileSystem)
            {
                _fileSystem = fileSystem;

                _messageHandler = new TestMessageHandler(fileSystem);
            }

            public override HttpClientHandler ClientHandler
            {
                get
                {
                    // should not be used!
                    return new HttpClientHandler();
                }
            }

            public override HttpMessageHandler MessageHandler
            {
                get
                {
                    return _messageHandler;
                }
            }
        }

        private class TestMessageHandler : HttpMessageHandler
        {
            private readonly PhysicalFileSystem _fileSystem;

            public TestMessageHandler(PhysicalFileSystem fileSystem)
            {
                _fileSystem = fileSystem;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var file = (PhysicalFile)_fileSystem.Get(request.RequestUri);

                if (!await file.Exists(NullLogger.Instance, cancellationToken))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                using (var stream = await file.GetStream(NullLogger.Instance, cancellationToken))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new TestHttpContent(stream)
                    };
                }
            }
        }

        private class TestHttpContent : HttpContent
        {
            private MemoryStream _stream;

            public TestHttpContent(Stream stream)
            {
                _stream = new MemoryStream();
                stream.CopyTo(_stream);
                _stream.Seek(0, SeekOrigin.Begin);
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return _stream.CopyToAsync(stream);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = (long)_stream.Length;
                return true;
            }
        }

        private class TestHttpSource : HttpSource
        {
            private PhysicalFileSystem _fileSystem;

            public TestHttpSource(PackageSource source, Func<Task<HttpHandlerResource>> messageHandlerFactory, PhysicalFileSystem fileSystem)
                : base(source, messageHandlerFactory)
            {
                _fileSystem = fileSystem;
            }

            protected override Stream TryReadCacheFile(string uri, TimeSpan maxAge, string cacheFile)
            {
                var file = (PhysicalFile)_fileSystem.Get(UriUtility.CreateUri(uri));

                if (file.Exists(NullLogger.Instance, CancellationToken.None).Result)
                {
                    using (var stream = file.GetStream(NullLogger.Instance, CancellationToken.None).Result)
                    {
                        var memoryStream = new MemoryStream();
                        stream.CopyTo(memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        return memoryStream;
                    }
                }
                else
                {
                    return null;
                }
            }
        }
    }
}