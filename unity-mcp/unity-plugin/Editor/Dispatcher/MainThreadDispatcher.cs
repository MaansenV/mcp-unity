#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Thread-safe dispatcher for executing actions on the Unity Main Thread.
    /// Uses EditorApplication.update to process queued actions.
    /// </summary>
    [InitializeOnLoad]
    public static class MainThreadDispatcher
    {
        static readonly ConcurrentQueue<Action> s_Queue = new ConcurrentQueue<Action>();
        static int s_MainThreadId;
        const int MaxActionsPerFrame = 100;

        static MainThreadDispatcher()
        {
            s_MainThreadId = Environment.CurrentManagedThreadId;
            EditorApplication.update += ProcessQueue;
        }

        /// <summary>
        /// Queue an action to run on the main thread (fire and forget).
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;
            s_Queue.Enqueue(action);
        }

        /// <summary>
        /// Execute an action on the main thread and return a Task.
        /// If already on main thread, executes synchronously.
        /// </summary>
        public static Task RunAsync(Action action)
        {
            if (action == null) return Task.CompletedTask;

            if (Environment.CurrentManagedThreadId == s_MainThreadId)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        public static Task<object?> RunAsync(Func<object?> action)
        {
            if (action == null) return Task.FromResult<object?>(null);

            if (Environment.CurrentManagedThreadId == s_MainThreadId)
            {
                return Task.FromResult(action());
            }

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Enqueue(() =>
            {
                try
                {
                    tcs.TrySetResult(action());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        /// <summary>
        /// Execute a function on the main thread and return a Task with the result.
        /// If already on main thread, executes synchronously.
        /// </summary>
        public static Task<T> RunAsync<T>(Func<T> action)
        {
            if (action == null) return Task.FromResult(default(T)!);

            if (Environment.CurrentManagedThreadId == s_MainThreadId)
            {
                return Task.FromResult(action());
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Enqueue(() =>
            {
                try
                {
                    tcs.TrySetResult(action());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        /// <summary>
        /// Alias for RunAsync(Action). Execute an action on the main thread.
        /// </summary>
        public static Task EnqueueAsync(Action action) => RunAsync(action);

        public static Task<object?> EnqueueAsync(Func<object?> action) => RunAsync(action);

        /// <summary>
        /// Alias for RunAsync(Func&lt;T&gt;). Execute a function on the main thread.
        /// </summary>
        public static Task<T> EnqueueAsync<T>(Func<T> action) => RunAsync(action);

        /// <summary>
        /// Process queued actions. Called by EditorApplication.update.
        /// Budget-limited to prevent editor freezes.
        /// </summary>
        public static void ProcessQueue()
        {
            int processed = 0;
            while (processed < MaxActionsPerFrame && s_Queue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
                processed++;
            }
        }
    }
}
#endif
