using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for getting the current Unity Profiler status.
    /// </summary>
    public class ProfilerGetStatusTool : McpToolBase
    {
        public ProfilerGetStatusTool()
        {
            Name = "profiler_get_status";
            Description = "Gets the current Unity Profiler status and memory usage";
        }

        /// <summary>
        /// Execute the ProfilerGetStatus tool with no parameters.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                long maxUsedMemoryBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                long monoUsedSizeBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = "Retrieved profiler status",
                    ["isEnabled"] = UnityEngine.Profiling.Profiler.enabled,
                    ["supported"] = UnityEngine.Profiling.Profiler.supported,
                    ["maxUsedMemoryMB"] = maxUsedMemoryBytes / 1048576.0,
                    ["monoUsedSizeMB"] = monoUsedSizeBytes / 1048576.0
                };
            }
            catch (System.Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error getting profiler status: {ex.Message}",
                    "profiler_error"
                );
            }
        }
    }
}
