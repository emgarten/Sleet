using System;
using NuGet.Common;

namespace Sleet
{
    public class ConsoleLogger : ILogger
    {
        private static readonly Object _lockObj = new object();

        public void LogDebug(string data)
        {
            Log(data);
        }

        public void LogError(string data)
        {
            Log(data, ConsoleColor.Red);
        }

        public void LogErrorSummary(string data)
        {
            throw new NotImplementedException();
        }

        public void LogInformation(string data)
        {
            Log(data);
        }

        public void LogInformationSummary(string data)
        {
            Log(data);
        }

        public void LogMinimal(string data)
        {
            Log(data);
        }

        public void LogSummary(string data)
        {
            Log(data);
        }

        public void LogVerbose(string data)
        {
            Log(data);
        }

        public void LogWarning(string data)
        {
            Log(data, ConsoleColor.Yellow);
        }

        private void Log(string message)
        {
            Log(message, color: null);
        }

        private void Log(string message, ConsoleColor? color)
        {
            lock (_lockObj)
            {
                if (color.HasValue)
                {
                    Console.ForegroundColor = color.Value;
                }

                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
    }
}
