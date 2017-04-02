using System;
using NuGet.Common;

namespace Sleet
{
    public class ConsoleLogger : ILogger, IDisposable
    {
        private static readonly object _lockObj = new object();
        private bool? _cursorVisibleOriginalState;
        private static readonly Lazy<bool> _allowAdvancedWrite = new Lazy<bool>(AllowAdvancedWrite);

        private string _lastCollapsedMessage;
        private string _lastIndicator;

        public LogLevel VerbosityLevel { get; set; }

        /// <summary>
        /// Collapse all messages below the minimal level.
        /// </summary>
        public bool CollapseMessages { get; set; }

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

        public void Dispose()
        {
            if (_cursorVisibleOriginalState.HasValue && _allowAdvancedWrite.IsValueCreated && _allowAdvancedWrite.Value)
            {
                Console.CursorVisible = _cursorVisibleOriginalState.Value;
            }
        }

        private void Log(LogLevel level, string message, ConsoleColor? color)
        {
            if ((int)level >= (int)VerbosityLevel)
            {
                var isCollapsed = CollapseMessages
                    && RuntimeEnvironmentHelper.IsWindows
                    && _allowAdvancedWrite.Value
                    && (int)level < (int)LogLevel.Minimal;

                // Replace or clear the color if needed
                var updatedColor = GetColor(color, isCollapsed);

                // Break up multi-line messages
                var messages = SplitMessages(message);

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

                        if (_allowAdvancedWrite.Value)
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

        private static ConsoleColor? GetColor(ConsoleColor? color, bool isCollapsed)
        {
            if (!color.HasValue && isCollapsed)
            {
                color = ConsoleColor.Gray;
            }

            if (!RuntimeEnvironmentHelper.IsWindows || !_allowAdvancedWrite.Value)
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

        private static bool IsCIMode()
        {
            var val = Environment.GetEnvironmentVariable("CI");

            if (!string.IsNullOrEmpty(val) && bool.TryParse(val, out var result))
            {
                return result;
            }

            return false;
        }

        private static bool AllowAdvancedWrite()
        {
            try
            {
                // Print normally on a CI
                if (!IsCIMode())
                {
                    // Verify the following actions do not throw. This can happen in web consoles.
                    var visible = Console.CursorVisible;
                    var color = Console.ForegroundColor;
                    var width = Console.WindowWidth;
                    Console.ResetColor();

                    return true;
                }
            }
            catch
            {
                // Fall back to normal console out.
            }

            return false;
        }

        private void HideCursor()
        {
            if (_cursorVisibleOriginalState == null && _allowAdvancedWrite.Value)
            {
                _cursorVisibleOriginalState = Console.CursorVisible;

                // Hide the cursor to improve overwrites
                Console.CursorVisible = false;
            }
        }
    }
}