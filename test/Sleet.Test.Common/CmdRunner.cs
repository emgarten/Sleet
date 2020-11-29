using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sleet.Test.Common
{
    /// <summary>
    /// Run an external exe
    /// </summary>
    public static class CmdRunner
    {
        /// <summary>
        /// Search the current directory and up for a path.
        /// </summary>
        /// <remarks>throws if not found</remarks>
        public static string GetPath(string relativePath)
        {
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var root = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (root != null)
            {
                var path = Path.Combine(root.FullName, relativePath);

                if (File.Exists(path))
                {
                    return path;
                }

                root = root.Parent;
            }

            throw new FileNotFoundException($"Unable to find {relativePath}, try running these tests from build.ps1");
        }

        /// <summary>
        /// Run an external process.
        /// </summary>
        public static Task<CmdRunnerResult> RunAsync(
            string exePath,
            string workingDirectory,
            string arguments)
        {
            return RunAsync(exePath, workingDirectory, arguments, envVars: null);
        }

        /// <summary>
        /// Run an external process.
        /// </summary>
        public static Task<CmdRunnerResult> RunAsync(
            string exePath,
            string workingDirectory,
            string arguments,
            Dictionary<string, string> envVars)
        {
            return Task.Factory.StartNew(() => Run(exePath, workingDirectory, arguments, envVars), TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Run an external process.
        /// </summary>
        public static CmdRunnerResult Run(
        string exePath,
        string workingDirectory,
        string arguments,
        Dictionary<string, string> envVars)
        {
            exePath = Path.GetFullPath(exePath);
            workingDirectory = Path.GetFullPath(workingDirectory);

            var processInfo = new ProcessStartInfo(exePath, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            SetEnvVarsOnProcess(envVars, processInfo);

            var output = new StringBuilder();
            var errors = new StringBuilder();

            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();

                var outputTask = ReadStreamAsync(process.StandardOutput, output);
                var errorTask = ReadStreamAsync(process.StandardError, errors);
                process.WaitForExit();

                Task.WaitAll(outputTask, errorTask);
                return new CmdRunnerResult(process.ExitCode, output.ToString(), errors.ToString());
            }
        }

        private static void SetEnvVarsOnProcess(Dictionary<string, string> envVars, ProcessStartInfo processInfo)
        {
            if (envVars?.Any() == true)
            {
                foreach (var envVar in envVars)
                {
#if !IS_CORECLR
                    processInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
#else
                    processInfo.Environment[envVar.Key] = envVar.Value;
#endif
                }
            }
        }

        private static async Task ReadStreamAsync(StreamReader streamReader, StringBuilder lines)
        {
            await Task.Yield();

            while (true)
            {
                var currentLine = await streamReader.ReadLineAsync();

                if (currentLine == null)
                {
                    break;
                }

                lines.AppendLine(currentLine);
            };
        }
    }

    public class CmdRunnerResult
    {
        /// <summary>
        /// Process exit code
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// True if Exit code is zero.
        /// </summary>
        public bool Success => ExitCode == 0;

        /// <summary>
        /// All output messages displayed.
        /// </summary>
        public string AllOutput => Output + Environment.NewLine + Errors;

        /// <summary>
        /// Non errors.
        /// </summary>
        public string Output { get; }

        /// <summary>
        /// Errors.
        /// </summary>
        public string Errors { get; }

        public CmdRunnerResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Errors = error;
        }
    }
}