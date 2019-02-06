using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    public class ConsoleLogger : LoggerBase, IDisposable
    {
        private static readonly object _lockObj = new object();
        private bool? _cursorVisibleOriginalState;
        private static readonly Lazy<bool> _isValidConsole = new Lazy<bool>(IsValidConsole);
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
            var color = GetColor(message);

            if (level >= (int)VerbosityLevel)
            {
                // Replace or clear the color if needed
                var updatedColor = GetColor(color, isCollapsed: false);

                // Break up multi-line messages
                var messages = SplitMessages(message.Message);

                lock (_lockObj)
                {
                    if (updatedColor.HasValue)
                    {
                        Console.ForegroundColor = updatedColor.Value;
                    }

                    for (var i = 0; i < messages.Length; i++)
                    {
                        // Modify message
                        var updatedMessage = messages[i];
                        updatedMessage = updatedMessage.TrimEnd() + Environment.NewLine;

                        // Write
                        Console.Write(updatedMessage);
                    }

                    if (updatedColor.HasValue)
                    {
                        Console.ResetColor();
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

        }

        private static ConsoleColor? GetColor(ILogMessage message)
        {
            ConsoleColor? color = null;

            if (message.Level == LogLevel.Error)
            {
                color = ConsoleColor.Red;
            }
            else if (message.Level == LogLevel.Warning)
            {
                color = ConsoleColor.Yellow;
            }

            return color;
        }

        private static ConsoleColor? GetColor(ConsoleColor? color, bool isCollapsed)
        {
            if (!color.HasValue && isCollapsed)
            {
                color = ConsoleColor.Gray;
            }

            if (!RuntimeEnvironmentHelper.IsWindows || !_isValidConsole.Value)
            {
                // Disallow colors for xplat
                color = null;
            }

            return color;
        }

        private static string[] SplitMessages(string message)
        {
            var messages = message.Split('\n');

            for (var i = 0; i < messages.Length; i++)
            {
                if (messages[i].EndsWith("\r"))
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

        private static bool IsValidConsole()
        {
            try
            {
#if IS_DESKTOP
                // For non-interactive console such as on CIs use normal logging.
                if (!Environment.UserInteractive)
                {
                    return false;
                }
#endif

                // Verify the console is valid and does not throw during any of these operations.
                // Some web consoles have issues with Console.* properties.
                if (!Console.CursorVisible)
                {
                    return false;
                }

                if (Console.WindowWidth < 50 || Console.WindowHeight < 20)
                {
                    return false;
                }

                var color = Console.ForegroundColor;
                Console.ResetColor();

                return true;
            }
            catch
            {
                // Fall back to normal console out.
            }

            return false;
        }

        private void HideCursor()
        {
            if (_cursorVisibleOriginalState == null && _isValidConsole.Value)
            {
                _cursorVisibleOriginalState = Console.CursorVisible;

                // Hide the cursor to improve overwrites
                Console.CursorVisible = false;
            }
        }
    }
}