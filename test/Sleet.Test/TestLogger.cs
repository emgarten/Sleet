using System.Collections.Concurrent;
using NuGet.Logging;

namespace Sleet.Test
{
    public class TestLogger : ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new ConcurrentQueue<string>();

        public void LogDebug(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogError(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogInformation(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogVerbose(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogWarning(string data)
        {
            Messages.Enqueue(data);
        }
    }
}
