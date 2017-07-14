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

        private string _lastCollapsedMessage;
        private string _lastIndicator;

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
                var isCollapsed = CollapseMessages
                    && RuntimeEnvironmentHelper.IsWindows
                    && level < (int)LogLevel.Minimal
                    && AllowAdvancedWrite();

                // Replace or clear the color if needed
                var updatedColor = GetColor(color, isCollapsed);

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

                        if (AllowAdvancedWrite())
                        {
                            // Hide the cursor for overwrites
                            HideCursor();

                            // Modify the message to overwrite if allowed.
                            updatedMessage = GetCollapsedMessage(messages[i], isCollapsed);
                        }
                        else
                        {
                            updatedMessage = updatedMessage.TrimEnd() + Environment.NewLine;
                        }

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
            if (_cursorVisibleOriginalState.HasValue && AllowAdvancedWrite())
            {
                Console.CursorVisible = _cursorVisibleOriginalState.Value;
            }
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

        private string GetCollapsedMessage(string message, bool isCollapsed)
        {
            var updatedMessage = message;

            if (isCollapsed)
            {
                var indicator = GetNextIndicator(_lastIndicator);
                updatedMessage = $"  [{indicator}] " + message.Trim();
                _lastIndicator = indicator;
            }

            var lastMessageWasCollapsed = !string.IsNullOrEmpty(_lastCollapsedMessage);

            var minLength = _lastCollapsedMessage?.TrimEnd().Length ?? 0;
            var maxLength = Math.Max(1, Console.WindowWidth - 1);

            if (!isCollapsed)
            {
                // Non-collapsed messages can take up more room.
                maxLength = Math.Max(maxLength, message.Length);
            }

            while (lastMessageWasCollapsed && updatedMessage.Length < minLength)
            {
                // Add extra spaces to overwrite the previous output
                updatedMessage += " ";
            }

            // Trim messages longer than the window. This causes odd output.
            if (updatedMessage.Length > maxLength)
            {
                updatedMessage = updatedMessage.Substring(0, maxLength);
            }

            if (lastMessageWasCollapsed)
            {
                updatedMessage = "\r" + updatedMessage;
            }

            if (isCollapsed)
            {
                // Move to the beginning of the line
                _lastCollapsedMessage = updatedMessage;
            }
            else
            {
                // Clear the last collapsed message
                _lastCollapsedMessage = null;
                _lastIndicator = null;
                updatedMessage += Environment.NewLine;
            }

            return updatedMessage;
        }

        private static string GetNextIndicator(string current)
        {
            // - \ | / - \ | / -
            if (string.IsNullOrEmpty(current))
            {
                return "-";
            }

            switch (current)
            {
                case "-":
                    return "\\";
                case "\\":
                    return "|";
                case "|":
                    return "/";
                case "/":
                    return "-";
                default:
                    return "-";
            }
        }

        /// <summary>
        /// Check if overwriting should be used.
        /// </summary>
        private bool AllowAdvancedWrite()
        {
            // Allow advanced console writes if this is not in debug mode and the console is interactive.
            return _isValidConsole.Value
                && !_isCITrue.Value
                && VerbosityLevel >= LogLevel.Information;
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