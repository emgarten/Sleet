using System;
using System.Linq;
using System.Threading;
using NuGet.Common;

namespace Sleet
{
    public class ConsoleLogger : ILogger, IDisposable
    {
        private static readonly object _lockObj = new object();
        private readonly bool _cursorVisibleOriginalState;
        private static readonly Lazy<bool> _ciMode = new Lazy<bool>(IsCIMode);

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

            _cursorVisibleOriginalState = Console.CursorVisible;

            // Hide the cursor to improve overwrites
            Console.CursorVisible = false;
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
            Console.CursorVisible = _cursorVisibleOriginalState;
        }

        private void Log(LogLevel level, string message, ConsoleColor? color)
        {
            if ((int)level >= (int)VerbosityLevel)
            {
                var isCollapsed = CollapseMessages
                    && RuntimeEnvironmentHelper.IsWindows
                    && !_ciMode.Value
                    && (int)level < (int)LogLevel.Minimal;

                if (!color.HasValue && isCollapsed)
                {
                    color = ConsoleColor.Gray;
                }

                if (!RuntimeEnvironmentHelper.IsWindows || _ciMode.Value)
                {
                    // Disallow colors for xplat
                    color = null;
                }

                // Break up multi-line messages
                var messages = message.Split('\n');

                for (var i = 0; i < messages.Length; i++)
                {
                    if (messages[i].EndsWith("\r"))
                    {
                        messages[i] = messages[i].TrimEnd('\r');
                    }
                }

                lock (_lockObj)
                {
                    if (color.HasValue)
                    {
                        Console.ForegroundColor = color.Value;
                    }

                    for (var i = 0; i < messages.Length; i++)
                    {
                        // Modify message
                        var updatedMessage = GetCollapsedMessage(messages[i], isCollapsed);

                        // Write
                        Console.Write(updatedMessage);
                    }

                    if (color.HasValue)
                    {
                        Console.ResetColor();
                    }
                }
            }
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
    }
}