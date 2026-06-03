using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace McpUnity.Utils
{
    /// <summary>
    /// Persists compilation state across domain reloads using SessionState.
    /// Survives domain reload via [InitializeOnLoad] + SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class ScriptCompilationTracker
    {
        private const string OperationIdKey = "MCP_Recompile_OperationId";
        private const string StateKey = "MCP_Recompile_State";
        private const string LogsKey = "MCP_Recompile_Logs";
        private const string ErrorsKey = "MCP_Recompile_Errors";
        private const string WarningsKey = "MCP_Recompile_Warnings";
        private const string StartedAtKey = "MCP_Recompile_StartedAt";
        private const string CompletedAtKey = "MCP_Recompile_CompletedAt";

        private const string StateRequested = "requested";
        private const string StateCompiling = "compiling";
        private const string StateCompleted = "completed";

        static ScriptCompilationTracker()
        {
            // Re-subscribe after domain reload
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.update += OnEditorUpdate;

            // Check if a compilation finished while we were reloading.
            // Use MainThreadDispatcher (update-based) so this runs even when
            // Unity Editor is not focused after domain reload.
            MainThreadDispatcher.Post(ProcessPendingAfterReload);
        }

        public static bool HasPendingOperation
        {
            get
            {
                string state = SessionState.GetString(StateKey, "");
                return state == StateRequested || state == StateCompiling;
            }
        }

        /// <summary>
        /// Start a new compilation operation. Returns an operationId.
        /// </summary>
        public static string ScheduleCompilation()
        {
            string operationId = Guid.NewGuid().ToString("N");

            SessionState.SetString(OperationIdKey, operationId);
            SessionState.SetString(StateKey, StateRequested);
            SessionState.SetString(LogsKey, "[]");
            SessionState.SetInt(ErrorsKey, 0);
            SessionState.SetInt(WarningsKey, 0);
            SessionState.SetString(StartedAtKey, DateTime.UtcNow.ToString("O"));
            SessionState.EraseString(CompletedAtKey);

            return operationId;
        }

        /// <summary>
        /// Create a "processing" response for an in-progress operation.
        /// </summary>
        public static JObject CreateProcessingResponse()
        {
            string operationId = SessionState.GetString(OperationIdKey, "");
            string state = SessionState.GetString(StateKey, StateRequested);

            string message = state == StateCompiling
                ? "Script compilation is running. Call recompile_scripts again to retrieve results."
                : "Script compilation is pending. Unity may be waiting for editor focus. Call recompile_scripts again.";

            return new JObject
            {
                ["success"] = true,
                ["status"] = "processing",
                ["operationId"] = operationId,
                ["message"] = message,
                ["logs"] = new JArray()
            };
        }

        /// <summary>
        /// Try to consume a completed result. Returns true if a completed result exists.
        /// Clears the operation after reading.
        /// </summary>
        public static bool TryConsumeCompletedResult(bool returnWithLogs, int logsLimit, out JObject result)
        {
            result = null;

            if (SessionState.GetString(StateKey, "") != StateCompleted)
                return false;

            string operationId = SessionState.GetString(OperationIdKey, "");
            int errors = SessionState.GetInt(ErrorsKey, 0);
            int warnings = SessionState.GetInt(WarningsKey, 0);

            JArray allLogs = ReadLogs();

            JArray logs = returnWithLogs
                ? new JArray(allLogs
                    .OrderByDescending(x => x["type"]?.ToString() == "Error")
                    .ThenByDescending(x => x["type"]?.ToString() == "Warning")
                    .Take(logsLimit))
                : new JArray();

            string message = errors > 0
                ? $"Recompilation completed with {errors} error(s) and {warnings} warning(s)"
                : $"Successfully recompiled all scripts with {warnings} warning(s)";

            result = new JObject
            {
                ["success"] = true,
                ["status"] = "completed",
                ["operationId"] = operationId,
                ["message"] = message,
                ["logs"] = logs
            };

            ClearOperation();
            return true;
        }

        // ── Compilation callbacks ──────────────────────────────────────

        private static void OnCompilationStarted(object _)
        {
            if (!HasPendingOperation) return;
            SessionState.SetString(StateKey, StateCompiling);
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (!HasPendingOperation) return;

            JArray logs = ReadLogs();
            int errors = SessionState.GetInt(ErrorsKey, 0);
            int warnings = SessionState.GetInt(WarningsKey, 0);

            foreach (var msg in messages)
            {
                if (msg.type == CompilerMessageType.Error) errors++;
                else if (msg.type == CompilerMessageType.Warning) warnings++;

                logs.Add(new JObject
                {
                    ["message"] = msg.message,
                    ["type"] = msg.type.ToString(),
                    ["file"] = msg.file ?? "",
                    ["line"] = msg.line,
                    ["column"] = msg.column
                });
            }

            SessionState.SetString(LogsKey, logs.ToString(Newtonsoft.Json.Formatting.None));
            SessionState.SetInt(ErrorsKey, errors);
            SessionState.SetInt(WarningsKey, warnings);
        }

        private static void OnCompilationFinished(object _)
        {
            if (!HasPendingOperation) return;
            MarkCompleted();
        }

        private static void OnEditorUpdate()
        {
            if (!HasPendingOperation) return;

            if (EditorApplication.isCompiling)
            {
                SessionState.SetString(StateKey, StateCompiling);
                return;
            }

            // Compilation finished but compilationFinished callback may have been missed
            string state = SessionState.GetString(StateKey, "");
            if (state == StateCompiling)
            {
                MarkCompleted();
            }
        }

        /// <summary>
        /// After domain reload, check if compilation already finished.
        /// The compilationFinished callback may have been lost during reload.
        /// </summary>
        private static void ProcessPendingAfterReload()
        {
            string state = SessionState.GetString(StateKey, "");

            if ((state == StateCompiling || state == StateRequested) && !EditorApplication.isCompiling)
            {
                MarkCompleted();
            }
        }

        // ── Private helpers ────────────────────────────────────────────

        private static void MarkCompleted()
        {
            SessionState.SetString(StateKey, StateCompleted);
            SessionState.SetString(CompletedAtKey, DateTime.UtcNow.ToString("O"));
        }

        private static JArray ReadLogs()
        {
            string raw = SessionState.GetString(LogsKey, "[]");
            try { return JArray.Parse(raw); }
            catch { return new JArray(); }
        }

        private static void ClearOperation()
        {
            SessionState.EraseString(OperationIdKey);
            SessionState.EraseString(StateKey);
            SessionState.EraseString(LogsKey);
            SessionState.EraseInt(ErrorsKey);
            SessionState.EraseInt(WarningsKey);
            SessionState.EraseString(StartedAtKey);
            SessionState.EraseString(CompletedAtKey);
        }
    }
}
