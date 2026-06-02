using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for disabling the Unity Profiler.
    /// </summary>
    public class ProfilerStopTool : McpToolBase
    {
        public ProfilerStopTool()
        {
            Name = "profiler_stop";
            Description = "Disables the Unity Profiler";
        }

        /// <summary>
        /// Execute the ProfilerStop tool with no parameters.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                UnityEngine.Profiling.Profiler.enabled = false;

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = "Profiler disabled"
                };
            }
            catch (System.Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error disabling profiler: {ex.Message}",
                    "profiler_error"
                );
            }
        }
    }
}
