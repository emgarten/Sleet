using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public class ConsoleLogger : LoggerBase, IDisposable
    {
        private static readonly object _lockObj = new object();
        private static readonly Lazy<bool> _isCITrue = new Lazy<bool>(IsCIMode);

        /// <summary>
        /// Collapse all messages below the minimal level.
        /// </summary>
        public bool CollapseMessages { get; set; }

        public ConsoleLogger()
            : this(LogLevel.Debug)
        {
        }

        public ConsoleLogger(LogLevel verbosityLevel)
            : base(verbosityLevel)
        {
            VerbosityLevel = verbosityLevel;
        }

        public override void Log(ILogMessage message)
        {
            var level = (int)message.Level;

            if (level >= (int)VerbosityLevel)
            {
                // Break up multi-line messages
                var messages = SplitMessages(message.Message);

                lock (_lockObj)
                {
                    for (var i = 0; i < messages.Length; i++)
                    {
                        // Modify message
                        var updatedMessage = messages[i];
                        updatedMessage = updatedMessage.TrimEnd() + Environment.NewLine;

                        // Write
                        Console.Write(updatedMessage);
                    }
                }
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);

            return Task.FromResult(true);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private static string[] SplitMessages(string message)
        {
            var messages = message.Split('\n');

            for (var i = 0; i < messages.Length; i++)
            {
                if (messages[i].EndsWith('\r'))
                {
                    messages[i] = messages[i].TrimEnd('\r');
                }
            }

            return messages;
        }

        private static bool IsCIMode()
        {
            var val = Environment.GetEnvironmentVariable("CI");

            if (!string.IsNullOrEmpty(val) && bool.TryParse(val, out var result))
            {
                return result;
            }

            return false;
        }
    }
}
