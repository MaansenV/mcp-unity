using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for enabling the Unity Profiler.
    /// </summary>
    public class ProfilerStartTool : McpToolBase
    {
        public ProfilerStartTool()
        {
            Name = "profiler_start";
            Description = "Enables the Unity Profiler";
        }

        /// <summary>
        /// Execute the ProfilerStart tool with no parameters.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                UnityEngine.Profiling.Profiler.enabled = true;

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = "Profiler enabled"
                };
            }
            catch (System.Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error enabling profiler: {ex.Message}",
                    "profiler_error"
                );
            }
        }
    }
}
