using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for getting Unity Profiler memory statistics.
    /// </summary>
    public class ProfilerGetMemoryStatsTool : McpToolBase
    {
        public ProfilerGetMemoryStatsTool()
        {
            Name = "profiler_get_memory_stats";
            Description = "Gets Unity Profiler memory statistics";
        }

        /// <summary>
        /// Execute the ProfilerGetMemoryStats tool with no parameters.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = "Retrieved profiler memory statistics",
                    ["totalAllocatedMemoryMB"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576.0,
                    ["totalReservedMemoryMB"] = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576.0,
                    ["totalUnusedReservedMemoryMB"] = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong() / 1048576.0,
                    ["monoUsedSizeMB"] = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576.0,
                    ["monoHeapSizeMB"] = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / 1048576.0
                };
            }
            catch (System.Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error getting profiler memory statistics: {ex.Message}",
                    "profiler_error"
                );
            }
        }
    }
}
