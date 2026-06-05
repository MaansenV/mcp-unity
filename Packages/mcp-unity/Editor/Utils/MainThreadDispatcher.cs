using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;

namespace McpUnity.Utils
{
    /// <summary>
    /// Thread-safe dispatcher that posts actions to Unity's main thread.
    /// Uses EditorApplication.update instead of delayCall so it works
    /// even when Unity Editor is not focused — delayCall is throttled/paused
    /// when Unity loses application focus, but update continues to fire.
    /// </summary>
    [InitializeOnLoad]
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> Actions = new ConcurrentQueue<Action>();
        private static bool _initialized;

        static MainThreadDispatcher()
        {
            if (_initialized) return;
            _initialized = true;
            EditorApplication.update += Pump;
        }

        /// <summary>
        /// Post an action to be executed on Unity's main thread at the next editor update.
        /// Thread-safe — can be called from any thread, including WebSocket background threads.
        /// </summary>
        public static void Post(Action action)
        {
            if (action == null) return;
            Actions.Enqueue(action);
        }

        /// <summary>
        /// Post an async action to be executed on Unity's main thread.
        /// Exceptions are caught and logged. Use this instead of Post(async () => ...) 
        /// to avoid unobserved async void exceptions.
        /// </summary>
        public static void PostAsync(Func<Task> asyncAction)
        {
            if (asyncAction == null) return;
            Post(() => _ = RunAsync(asyncAction));
        }

        /// <summary>
        /// Post an action to be executed after a specified number of editor updates.
        /// Useful for deferring execution to let WebSocket responses flush first.
        /// </summary>
        public static void PostAfterUpdates(int updateCount, Action action)
        {
            if (action == null || updateCount <= 0)
            {
                Post(action);
                return;
            }

            Post(() => _ = RunAfterUpdates(updateCount, action));
        }

        /// <summary>
        /// Post an async action to be executed after a specified number of editor updates.
        /// </summary>
        public static void PostAfterUpdatesAsync(int updateCount, Func<Task> asyncAction)
        {
            if (asyncAction == null || updateCount <= 0)
            {
                PostAsync(asyncAction);
                return;
            }

            Post(() => _ = RunAfterUpdatesAsync(updateCount, asyncAction));
        }

        private static async Task RunAsync(Func<Task> asyncAction)
        {
            try
            {
                await asyncAction();
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"MainThreadDispatcher async exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static async Task RunAfterUpdates(int updateCount, Action action)
        {
            for (int i = 0; i < updateCount; i++)
            {
                await Task.Yield();
            }
            try
            {
                action();
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"MainThreadDispatcher deferred exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static async Task RunAfterUpdatesAsync(int updateCount, Func<Task> asyncAction)
        {
            for (int i = 0; i < updateCount; i++)
            {
                await Task.Yield();
            }
            try
            {
                await asyncAction();
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"MainThreadDispatcher deferred async exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void Pump()
        {
            while (Actions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    McpLogger.LogError($"MainThreadDispatcher: Unhandled exception in dispatched action: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }
}
