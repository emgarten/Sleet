// Shared source file

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    internal static class CmdUtils
    {
        private static readonly object _consoleLock = new object();

        /// <summary>
        /// Throw if a required option does not exist.
        /// </summary>
        internal static void VerifyRequiredOptions(params CommandOption[] required)
        {
            // Validate parameters
            foreach (var requiredOption in required)
            {
                if (!requiredOption.HasValue())
                {
                    throw new ArgumentException($"Missing required parameter --{requiredOption.LongName}.");
                }
            }
        }

        /// <summary>
        /// Throw if more than one of the options exist.
        /// </summary>
        internal static void VerifyMutallyExclusiveOptions(params CommandOption[] exclusiveOptions)
        {
            var withValues = exclusiveOptions.Where(e => e.HasValue()).ToList();

            if (withValues.Count > 1)
            {
                throw new ArgumentException($"{string.Join(", ", withValues.Select(e => $"--{e.LongName}"))} may not be used together.");
            }
        }

        /// <summary>
        /// Throw if an option from both groups exist.
        /// </summary>
        internal static void VerifyMutallyExclusiveOptions(IEnumerable<CommandOption> a, IEnumerable<CommandOption> b)
        {
            var aValues = a.Where(e => e.HasValue()).ToList();
            var bValues = b.Where(e => e.HasValue()).ToList();

            if (aValues.Count > 0 && bValues.Count > 0)
            {
                throw new ArgumentException($"{aValues.First().LongName} may not be used with {bValues.First().LongName}.");
            }
        }

        /// <summary>
        /// Verify at least one of the options exists.
        /// </summary>
        internal static void VerifyOneOptionExists(params CommandOption[] options)
        {
            var withValues = options.Where(e => e.HasValue()).ToList();

            if (withValues.Count < 1)
            {
                throw new ArgumentException($"One of the following options must be specified: {string.Join(", ", options.Select(e => $"--{e.LongName}"))}");
            }
        }

        /// <summary>
        /// Log a message to the console.
        /// </summary>
        internal static void LogToConsole(LogLevel level, string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var color = GetColor(level);

            lock (_consoleLock)
            {
                // Colorize
                if (color.HasValue)
                {
                    Console.ForegroundColor = color.Value;
                }

                // Write message
                Console.WriteLine(message);

                if (color.HasValue)
                {
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// Colorize warnings and errors.
        /// </summary>
        internal static ConsoleColor? GetColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return ConsoleColor.Red;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
            }

            return null;
        }

        /// <summary>
        /// True if ASSEMBLY_DEBUG is 1
        /// </summary>
        internal static bool IsDebugModeEnabled()
        {
            var varName = $"{GetAssemblyName()}_DEBUG".ToUpperInvariant();
            return Environment.GetEnvironmentVariable(varName) == "1";
        }

        /// <summary>
        /// Assembly name without extension.
        /// </summary>
        internal static string GetAssemblyName()
        {
            return GetAssembly().GetName().Name;
        }

        /// <summary>
        /// Assembly version.
        /// </summary>
        internal static Version GetAssemblyVersion()
        {
            return GetAssembly().GetName().Version;
        }

        /// <summary>
        /// Returns the assembly containing this method.
        /// </summary>
        internal static Assembly GetAssembly()
        {
            return typeof(CmdUtils).GetTypeInfo().Assembly;
        }

        /// <summary>
        /// Wait for the debugger to attach if --debug is set.
        /// </summary>
        [Conditional("DEBUG")]
        internal static void LaunchDebuggerIfSet(ref string[] args, ILogger logger)
        {
            if (string.Equals("--debug", args?.FirstOrDefault(), StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();

#if IS_DESKTOP
                Console.WriteLine($"Waiting for debugger to attach on process: {Process.GetCurrentProcess().Id}");
                Console.ReadLine();
#else
                Debugger.Launch();
#endif
            }
        }
    }
}
