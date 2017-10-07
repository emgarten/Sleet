using System;
using System.Net;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Sleet
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var logLevel = LogLevel.Information;

            if (CmdUtils.IsDebugModeEnabled())
            {
                logLevel = LogLevel.Debug;
            }

            using (var log = new ConsoleLogger(logLevel))
            {
                var task = MainCore(args, log);
                return task.Result;
            }
        }

        public static Task<int> MainCore(string[] args, ILogger log)
        {
            CmdUtils.LaunchDebuggerIfSet(ref args, log);

            var app = new CommandLineApplication()
            {
                Name = "sleet",
                FullName = "Sleet"
            };
            app.HelpOption(Constants.HelpOption);
            app.VersionOption("--version", AssemblyVersionHelper.GetVersion().ToFullVersionString());

            Configure();

            InitAppCommand.Register(app, log);
            PushAppCommand.Register(app, log);
            DeleteAppCommand.Register(app, log);
            ValidateAppCommand.Register(app, log);
            StatsAppCommand.Register(app, log);
            CreateConfigAppCommand.Register(app, log);
            DestroyAppCommand.Register(app, log);
            DownloadAppCommand.Register(app, log);
            RecreateAppCommand.Register(app, log);
            FeedSettingsAppCommand.Register(app, log);

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 1;
            });

            var exitCode = 1;

            try
            {
                exitCode = app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                ex.Command.ShowHelp();
            }
            catch (Exception ex)
            {
                ExceptionUtils.LogException(ex, log);
            }

            return Task.FromResult(exitCode);
        }

        private static void Configure()
        {
            // Set connection limit
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                ServicePointManager.DefaultConnectionLimit = 64;
            }
            else
            {
                // Keep mono limited to a single download to avoid issues.
                ServicePointManager.DefaultConnectionLimit = 1;
            }

            // Limit SSL
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls12;
        }
    }
}