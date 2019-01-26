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
            var steps = GetSteps(context);
            var tasks = steps.Select(e => new Func<Task>(() => e.RunAsync(changeContext))).ToList();

            // Run each service on its own thread and in parallel
            // Services with depenencies will pre-fetch files that will be used later
            // and then wait until the other services have completed.
            await TaskUtils.RunAsync(tasks, useTaskRun: true, maxThreads: steps.Count, token: CancellationToken.None);
        }

        /// <summary>
        /// Build pipeline steps.
        /// </summary>
        private static List<SleetStep> GetSteps(SleetContext context)
        {
            var result = new List<SleetStep>();

            var catalog = new SleetStep(GetCatalogService(context));
            result.Add(catalog);

            if (context.SourceSettings.SymbolsEnabled)
            {
                result.Add(new SleetStep(new Symbols(context)));
            }

            result.Add(new SleetStep(new FlatContainer(context)));
            result.Add(new SleetStep(new AutoComplete(context)));
            result.Add(new SleetStep(new PackageIndex(context)));

            // Registration depends on catalog pages
            var registrations = new SleetStep(new Registrations(context), catalog);
            result.Add(registrations);

            //// Search depends on registation files
            var search = new SleetStep(new Search(context), registrations);
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
            public List<SleetStep> Dependencies { get; } = new List<SleetStep>();

            public SleetStep(ISleetService service)
            {
                Service = service;
            }

            public SleetStep(ISleetService service, SleetStep dependency)
            {
                Service = service;
                Dependencies.Add(dependency);
            }

            public async Task WaitAsync()
            {
                // Run with a simple spin lock to avoid problems with SemaphoreSlim
                while (!_done)
                {
                    await Task.Delay(10);
                }
            }

            public async Task RunAsync(SleetOperations operations)
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

                    // Update the service
                    await Service.ApplyOperationsAsync(operations);

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