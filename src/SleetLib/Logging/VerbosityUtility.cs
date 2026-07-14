using System;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// Maps MSBuild/dotnet-style verbosity level names to the matching <see cref="LogLevel"/> threshold.
    /// Messages at or above the returned level are written to the console.
    /// </summary>
    public static class VerbosityUtility
    {
        /// <summary>
        /// Parse a verbosity level name into a <see cref="LogLevel"/> threshold.
        /// Supported values (case-insensitive): quiet (q), minimal (m), normal (n),
        /// detailed (d), diagnostic (diag).
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the value is not a known verbosity level.</exception>
        public static LogLevel GetLogLevel(string verbosity)
        {
            if (TryGetLogLevel(verbosity, out var level))
            {
                return level;
            }

            throw new ArgumentException(
                $"Invalid verbosity: '{verbosity}'. Valid values are: quiet, minimal, normal, detailed, diagnostic.",
                nameof(verbosity));
        }

        /// <summary>
        /// Try to parse a verbosity level name into a <see cref="LogLevel"/> threshold.
        /// </summary>
        /// <returns>True if the value was a known verbosity level.</returns>
        public static bool TryGetLogLevel(string verbosity, out LogLevel level)
        {
            switch (verbosity?.Trim().ToLowerInvariant())
            {
                case "quiet":
                case "q":
                    level = LogLevel.Warning;
                    return true;
                case "minimal":
                case "m":
                    level = LogLevel.Minimal;
                    return true;
                case "normal":
                case "n":
                    level = LogLevel.Information;
                    return true;
                case "detailed":
                case "d":
                    level = LogLevel.Verbose;
                    return true;
                case "diagnostic":
                case "diag":
                    level = LogLevel.Debug;
                    return true;
                default:
                    level = LogLevel.Information;
                    return false;
            }
        }
    }
}
