using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Sleet.Test.Common
{
    /// <summary>
    /// Finds the services that local-env runs, see local-env/README.md.
    /// </summary>
    public static class LocalEnvironment
    {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);
        private static readonly ConcurrentDictionary<int, Lazy<bool>> Listening = new();

        /// <summary>
        /// True if something is listening on the port on 127.0.0.1. The result is cached for the test process.
        /// </summary>
        public static bool IsListening(int port)
        {
            return Listening.GetOrAdd(port, p => new Lazy<bool>(() => CanConnect(p))).Value;
        }

        private static bool CanConnect(int port)
        {
            // Connect on the thread pool so that blocking here can't deadlock a synchronization context
            return Task.Run(async () =>
            {
                using var client = new TcpClient();
                using var timeout = new CancellationTokenSource(ConnectTimeout);

                try
                {
                    await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
                    return true;
                }
                catch (SocketException)
                {
                    return false;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }).GetAwaiter().GetResult();
        }
    }
}
