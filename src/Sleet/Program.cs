using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Logging;

namespace Sleet
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var log = new ConsoleLogger();

            var task = MainCore(args, log);
            return task.Result;
        }

        public static Task<int> MainCore(string[] args, ILogger log)
        {
#if DEBUG
            if (args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
                while (!Debugger.IsAttached)
                {
                }

                Debugger.Break();
            }
#endif

            var app = new CommandLineApplication();
            app.Name = "sleet";
            app.FullName = "Sleet";
            app.HelpOption("-h|--help");
            app.VersionOption("--version", typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());

            InitCommand.Register(app, log);
            PushCommand.Register(app);
            DeleteCommand.Register(app);

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });

            var exitCode = 0;

            try
            {
                exitCode = app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                ex.Command.ShowHelp();
            }
            catch (AggregateException ex)
            {
                exitCode = 1;

                foreach (var inner in ex.InnerExceptions)
                {
                    log.LogError(inner.Message);
                    log.LogDebug(inner.ToString());
                }
            }
            catch (Exception ex)
            {
                exitCode = 1;
                log.LogError(ex.Message);
                log.LogDebug(ex.ToString());
            }

            return Task.FromResult(exitCode);
        }
    }
}
