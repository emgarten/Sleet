using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sleet
{
    public static class SleetUtility
    {
        /// <summary>
        /// Create a dictionary of packages by id
        /// </summary>
        public static Dictionary<string, List<T>> GetPackageSetsById<T>(IEnumerable<T> packages, Func<T, string> getId)
        {
            var result = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in packages)
            {
                var id = getId(package);

                List<T> list = null;
                if (!result.TryGetValue(id, out list))
                {
                    list = new List<T>(1);
                    result.Add(id, list);
                }

                list.Add(package);
            }

            return result;
        }

        /// <summary>
        /// Main entry point for updating the feed.
        /// All add/remove operations can be added to a single changeContext and applied in
        /// a single call using this method.
        /// </summary>
        public static async Task ApplyPackageChangesAsync(SleetContext context, SleetOperations changeContext)
        {
            using (var timer = PerfEntryWrapper.CreateSummaryTimer("Updated all files locally. Total time: {0}", context.PerfTracker))
            {
                var steps = GetSteps(context);
                var tasks = steps.Select(e => new Func<Task>(() => e.RunAsync(changeContext, context.PerfTracker))).ToList();

                // Run each service on its own thread and in parallel
                // Services with depenencies will pre-fetch files that will be used later
                // and then wait until the other services have completed.
                await TaskUtils.RunAsync(tasks, useTaskRun: true, maxThreads: steps.Count, token: CancellationToken.None);
            }
        }

        /// <summary>
        /// Build pipeline steps.
        /// </summary>
        private static List<SleetStep> GetSteps(SleetContext context)
        {
            var result = new List<SleetStep>();

            var catalog = new SleetStep(GetCatalogService(context), SleetPerfGroup.Catalog);
            result.Add(catalog);

            if (context.SourceSettings.SymbolsEnabled)
            {
                result.Add(new SleetStep(new Symbols(context), SleetPerfGroup.Symbols));
            }

            result.Add(new SleetStep(new FlatContainer(context), SleetPerfGroup.FlatContainer));
            result.Add(new SleetStep(new AutoComplete(context), SleetPerfGroup.AutoComplete));
            result.Add(new SleetStep(new PackageIndex(context), SleetPerfGroup.PackageIndex));

            // Registration depends on catalog pages
            var registrations = new SleetStep(new Registrations(context), SleetPerfGroup.Registration, catalog);
            result.Add(registrations);

            // Search depends on registation files
            var search = new SleetStep(new Search(context), SleetPerfGroup.Search, registrations);
            result.Add(search);

            return result;
        }

        /// <summary>
        /// PreLoad, Wait for dependencies, Run
        /// </summary>
        private class SleetStep
        {
            private bool _done = false;

            public ISleetService Service { get; }
            public SleetPerfGroup PerfGroup { get; }
            public List<SleetStep> Dependencies { get; } = new List<SleetStep>();

            public SleetStep(ISleetService service, SleetPerfGroup perfGroup)
            {
                Service = service;
            }

            public SleetStep(ISleetService service, SleetPerfGroup perfGroup, SleetStep dependency)
            {
                Service = service;
                PerfGroup = perfGroup;
                Dependencies.Add(dependency);
            }

            public async Task WaitAsync()
            {
                // Run with a simple spin lock to avoid problems with SemaphoreSlim
                while (!_done)
                {
                    await Task.Delay(100);
                }
            }

            public async Task RunAsync(SleetOperations operations, IPerfTracker perfTracker)
            {
                try
                {
                    // Pre load while waiting for dependencies
                    await Service.PreLoadAsync(operations);

                    if (Dependencies.Count > 0)
                    {
                        // Wait for dependencies
                        // await Task.WhenAll(Dependencies.Select(e => e.Semaphore.WaitAsync()));
                        foreach (var dep in Dependencies)
                        {
                            await dep.WaitAsync();
                        }
                    }

                    var name = Service.GetType().ToString().Split('.').Last();
                    var message = $"Updated {name} in " + "{0}";
                    using (var timer = PerfEntryWrapper.CreateSummaryTimer(message, perfTracker))
                    {
                        // Update the service
                        await Service.ApplyOperationsAsync(operations);
                    }
                }
                finally
                {
                    // Complete
                    _done = true;
                }
            }
        }

        /// <summary>
        /// Retrieve the catalog service based on the settings.
        /// </summary>
        private static ISleetService GetCatalogService(SleetContext context)
        {
            ISleetService catalog = null;
            if (context.SourceSettings.CatalogEnabled)
            {
                // Full catalog that is written to the feed
                catalog = new Catalog(context);
            }
            else
            {
                // In memory catalog that is not written to the feed
                catalog = new VirtualCatalog(context);
            }

            return catalog;
        }
    }
}