using System;
using NuGet.Common;

namespace Sleet
{
    public class ConsoleLogger : ILogger
    {
        private static readonly Object _lockObj = new object();

        public LogLevel VerbosityLevel { get; set; }

        public ConsoleLogger()
            : this(LogLevel.Debug)
        {
        }

        public ConsoleLogger(LogLevel level)
        {
            VerbosityLevel = level;
        }

        public void LogDebug(string data)
        {
            Log(LogLevel.Debug, data);
        }

        public void LogError(string data)
        {
            Log(LogLevel.Error, data, ConsoleColor.Red);
        }

        public void LogErrorSummary(string data)
        {
            throw new NotImplementedException();
        }

        public void LogInformation(string data)
        {
            Log(LogLevel.Information, data);
        }

        public void LogInformationSummary(string data)
        {
            Log(LogLevel.Information, data);
        }

        public void LogMinimal(string data)
        {
            Log(LogLevel.Minimal, data);
        }

        public void LogSummary(string data)
        {
            Log(LogLevel.Information, data);
        }

        public void LogVerbose(string data)
        {
            Log(LogLevel.Verbose, data);
        }

        public void LogWarning(string data)
        {
            Log(LogLevel.Warning, data, ConsoleColor.Yellow);
        }

        private void Log(LogLevel level, string message)
        {
            Log(level, message, color: null);
        }

        private void Log(LogLevel level, string message, ConsoleColor? color)
        {
            if ((int)level >= (int)VerbosityLevel)
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
}
