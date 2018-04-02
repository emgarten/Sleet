// Shared source file

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sleet
{
    internal class TaskUtils
    {
#if NET45
        private static readonly Task InternalCompletedTask = Task.FromResult<object>(null);
#endif

        public static Task CompletedTask
        {
            get
            {
#if !NET45
                return Task.CompletedTask;
#else
                return InternalCompletedTask;
#endif
            }
        }

        /// <summary>
        /// Run tasks in parallel.
        /// </summary>
        public static Task RunAsync(IEnumerable<Func<Task>> tasks)
        {
            return RunAsync(
                tasks: tasks.Select(GetFuncWithReturnValue),
                token: CancellationToken.None);
        }

        /// <summary>
        /// Run tasks in parallel.
        /// </summary>
        public static Task RunAsync(IEnumerable<Func<Task>> tasks, CancellationToken token)
        {
            return RunAsync(
                tasks: tasks.Select(GetFuncWithReturnValue),
                useTaskRun: false,
                token: token);
        }

        /// <summary>
        /// Run tasks in parallel.
        /// </summary>
        public static Task RunAsync(IEnumerable<Func<Task>> tasks, bool useTaskRun, CancellationToken token)
        {
            return RunAsync(
                tasks: tasks.Select(GetFuncWithReturnValue),
                useTaskRun: false,
                token: token);
        }

        /// <summary>
        /// Run tasks in parallel.
        /// </summary>
        public static Task RunAsync(IEnumerable<Func<Task>> tasks, bool useTaskRun, int maxThreads, CancellationToken token)
        {
            return RunAsync(
                tasks: tasks.Select(GetFuncWithReturnValue),
                useTaskRun: false,
                maxThreads: maxThreads,
                token: token);
        }

        /// <summary>
        /// Run tasks in parallel and returns the results in the original order.
        /// </summary>
        public static Task<T[]> RunAsync<T>(IEnumerable<Func<Task<T>>> tasks)
        {
            return RunAsync(tasks, CancellationToken.None);
        }


        /// <summary>
        /// Run tasks in parallel and returns the results in the original order.
        /// </summary>
        public static Task<T[]> RunAsync<T>(IEnumerable<Func<Task<T>>> tasks, CancellationToken token)
        {
            return RunAsync(tasks, useTaskRun: false, token: token);
        }

        /// <summary>
        /// Run tasks in parallel and returns the results in the original order.
        /// </summary>
        public static Task<T[]> RunAsync<T>(IEnumerable<Func<Task<T>>> tasks, bool useTaskRun, CancellationToken token)
        {
            var maxThreads = Environment.ProcessorCount * 2;

            return RunAsync(tasks, useTaskRun, maxThreads, token);
        }

        /// <summary>
        /// Run tasks in parallel and returns the results in the original order.
        /// </summary>
        public static Task<T[]> RunAsync<T>(IEnumerable<Func<Task<T>>> tasks, bool useTaskRun, int maxThreads, CancellationToken token)
        {
            return RunAsync(tasks, useTaskRun, maxThreads, DefaultProcess, token);
        }

        /// <summary>
        /// Run tasks in parallel and returns the results in the original order.
        /// </summary>
        public async static Task<R[]> RunAsync<T, R>(IEnumerable<Func<Task<T>>> tasks, bool useTaskRun, int maxThreads, Func<Task<T>, Task<R>> process, CancellationToken token)
        {
            var toRun = new ConcurrentQueue<WorkItem<T>>();

            var index = 0;
            foreach (var task in tasks)
            {
                // Create work items, save the original position.
                toRun.Enqueue(new WorkItem<T>(task, index));
                index++;
            }

            var totalCount = index;

            // Create an array for the results, at this point index is the count.
            var results = new R[totalCount];

            List<Task> threads = null;
            var taskCount = GetAdditionalThreadCount(maxThreads, totalCount);

            if (taskCount > 0)
            {
                threads = new List<Task>(taskCount);

                // Create long running tasks to run work on.
                for (var i = 0; i < taskCount; i++)
                {
                    Task task = null;

                    if (useTaskRun)
                    {
                        // Start a new task
                        task = Task.Run(() => RunTaskAsync(toRun, results, process, token));
                    }
                    else
                    {
                        // Run directly
                        task = RunTaskAsync(toRun, results, process, token);
                    }

                    threads.Add(task);
                }
            }

            // Run tasks on the current thread
            // This is used both for parallel and non-parallel.
            await RunTaskAsync(toRun, results, process, token);

            // After all work completes on this thread, wait for the rest.
            if (threads != null)
            {
                await Task.WhenAll(threads);
            }

            return results;
        }

        private static int GetAdditionalThreadCount(int maxThreads, int index)
        {
            // Number of threads total
            var x = Math.Min(index, maxThreads);

            // Remove one for the current thread
            x--;

            // Avoid -1
            x = Math.Max(0, x);

            return x;
        }

        /// <summary>
        /// Run tasks on a single thread.
        /// </summary>
        private static async Task RunTaskAsync<T, R>(ConcurrentQueue<WorkItem<T>> toRun, R[] results, Func<Task<T>, Task<R>> process, CancellationToken token)
        {
            // Run until cancelled or we are out of work.
            while (!token.IsCancellationRequested && toRun.TryDequeue(out var item))
            {
                var result = await process(item.Item());
                results[item.Index] = result;
            }
        }

        private static Task<T> DefaultProcess<T>(Task<T> result)
        {
            return result;
        }

        private static Func<Task<bool>> GetFuncWithReturnValue(Func<Task> task)
        {
            return new Func<Task<bool>>(() =>
            {
                return RunWithReturnValue(task);
            });
        }

        private static async Task<bool> RunWithReturnValue(Func<Task> task)
        {
            await task();
            return true;
        }

        /// <summary>
        /// Contains a Func to run and the original position in the queue.
        /// </summary>
        private sealed class WorkItem<T>
        {
            internal Func<Task<T>> Item { get; }

            internal int Index { get; }

            public WorkItem(Func<Task<T>> item, int index)
            {
                Item = item;
                Index = index;
            }
        }
    }
}
