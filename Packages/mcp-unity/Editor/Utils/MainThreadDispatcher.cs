using System;
using System.Collections.Concurrent;
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
