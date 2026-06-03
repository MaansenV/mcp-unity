using McpUnity.Utils;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool to recompile all scripts in the Unity project.
    /// Non-blocking: returns "processing" immediately, results available on next call.
    /// Survives domain reload via SessionState persistence.
    /// </summary>
    public class RecompileScriptsTool : McpToolBase
    {
        public RecompileScriptsTool()
        {
            Name = "recompile_scripts";
            Description = "Recompiles all scripts in the Unity project. Returns immediately; call again to retrieve results.";
            IsAsync = false;
        }

        public override JObject Execute(JObject parameters)
        {
            var returnWithLogs = GetBoolParameter(parameters, "returnWithLogs", true);
            var logsLimit = ClampInt(GetIntParameter(parameters, "logsLimit", 100), 0, 1000);

            // 1) If a completed result exists, return it
            if (ScriptCompilationTracker.TryConsumeCompletedResult(returnWithLogs, logsLimit, out JObject completed))
            {
                McpLogger.LogInfo("Recompilation completed. Returning cached result.");
                return completed;
            }

            // 2) If compilation is already pending/running, return current status
            if (ScriptCompilationTracker.HasPendingOperation)
            {
                McpLogger.LogInfo("Recompilation already in progress. Returning processing status.");
                return ScriptCompilationTracker.CreateProcessingResponse();
            }

            // 3) Start a new compilation operation
            McpLogger.LogInfo("Starting new script recompilation.");
            string operationId = ScriptCompilationTracker.ScheduleCompilation();

            // Schedule actual compilation AFTER this response goes out,
            // so the WebSocket response is sent before domain reload kills the connection.
            // Use MainThreadDispatcher (update-based) so compilation starts even when
            // Unity Editor is not focused.
            McpUnity.Utils.MainThreadDispatcher.Post(() =>
            {
                if (!EditorApplication.isCompiling)
                {
                    McpLogger.LogInfo("Requesting script compilation via CompilationPipeline.");
                    CompilationPipeline.RequestScriptCompilation();
                }
                else
                {
                    McpLogger.LogInfo("Unity is already compiling. Waiting for current compilation to finish.");
                }
            });

            return new JObject
            {
                ["success"] = true,
                ["status"] = "processing",
                ["operationId"] = operationId,
                ["message"] = "Script compilation requested. " +
                    "Call recompile_scripts again to check the result.",
                ["logs"] = new JArray()
            };
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static bool GetBoolParameter(JObject parameters, string key, bool defaultValue)
        {
            if (parameters?[key] != null && bool.TryParse(parameters[key].ToString(), out bool value))
                return value;
            return defaultValue;
        }

        private static int GetIntParameter(JObject parameters, string key, int defaultValue)
        {
            if (parameters?[key] != null && int.TryParse(parameters[key].ToString(), out int value))
                return value;
            return defaultValue;
        }

        private static int ClampInt(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
