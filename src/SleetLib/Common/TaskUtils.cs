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
                runType: TaskRunType.TaskLongRunning,
                token: token);
        }

        /// <summary>
        /// Run tasks in parallel.
        /// </summary>
        public static Task RunAsync(IEnumerable<Func<Task>> tasks, TaskRunType runType, CancellationToken token)
        {
            return RunAsync(
                tasks: tasks.Select(GetFuncWithReturnValue),
                runType: runType,
                token: token);
        }

        /// <summary>
        /// Run tasks in parallel.
        /// </summary>
        public static Task RunAsync(IEnumerable<Func<Task>> tasks, TaskRunType runType, int maxThreads, CancellationToken token)
        {
            return RunAsync(
                tasks: tasks.Select(GetFuncWithReturnValue),
                runType: runType,
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
            return RunAsync(tasks, TaskRunType.TaskLongRunning, token);
        }

        /// <summary>
        /// Run tasks in parallel and returns the results in the original order.
        /// </summary>
        public static Task<T[]> RunAsync<T>(IEnumerable<Func<Task<T>>> tasks, TaskRunType runType, CancellationToken token)
        {
            var maxThreads = Environment.ProcessorCount * 2;

            return RunAsync(tasks, runType, maxThreads, token);
        }

        /// <summary>
        /// Run tasks in parallel and returns the results in the original order.
        /// </summary>
        public async static Task<T[]> RunAsync<T>(IEnumerable<Func<Task<T>>> tasks, TaskRunType runType, int maxThreads, CancellationToken token)
        {
            var toRun = new ConcurrentQueue<WorkItem<T>>();

            var index = 0;
            foreach (var task in tasks)
            {
                // Create work items, save the original position.
                toRun.Enqueue(new WorkItem<T>(task, index));
                index++;
            }

            // Create an array for the results, at this point index is the count.
            var results = new T[index];

            List<Task> threads = null;
            var taskCount = GetAdditionalThreadCount(maxThreads, index);

            if (taskCount > 0)
            {
                threads = new List<Task>(taskCount);

                // Create long running tasks to run work on.
                for (var i = 0; i < taskCount; i++)
                {
                    Task task = null;

                    if (runType == TaskRunType.TaskLongRunning || runType == TaskRunType.TaskRun)
                    {
                        var options = (runType == TaskRunType.TaskLongRunning ? TaskCreationOptions.LongRunning : TaskCreationOptions.None);

                        // Start a new task
                        task = Task.Factory.StartNew(async _ =>
                        {
                            await RunTaskAsync(toRun, results, token);
                        },
                        options,
                        CancellationToken.None);
                    }
                    else
                    {
                        // Run directly
                        task = RunTaskAsync(toRun, results, token);
                    }

                    threads.Add(task);
                }
            }

            // Run tasks on the current thread
            // This is used both for parallel and non-parallel.
            await RunTaskAsync(toRun, results, token);

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
        private static async Task RunTaskAsync<T>(ConcurrentQueue<WorkItem<T>> toRun, T[] results, CancellationToken token)
        {
            // Run until cancelled or we are out of work.
            while (!token.IsCancellationRequested && toRun.TryDequeue(out var item))
            {
                results[item.Index] = await item.Item();
            }
        }

        private static Func<Task<bool>> GetFuncWithReturnValue(Func<Task> task)
        {
            return new Func<Task<bool>>(() => RunWithReturnValue(task));
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

        internal enum TaskRunType
        {
            /// <summary>
            /// Without TaskRun
            /// </summary>
            Default = 0,

            /// <summary>
            /// Use Task.Run
            /// </summary>
            TaskRun = 1,

            /// <summary>
            /// Use Task.Run with LongRunning
            /// </summary>
            TaskLongRunning = 2,
        }
    }
}
