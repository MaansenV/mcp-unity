using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool to capture the current frame's performance data.
    /// Returns deltaTime, FPS, frame count, and time since startup.
    /// </summary>
    public class ProfilerCaptureFrameTool : McpToolBase
    {
        public ProfilerCaptureFrameTool()
        {
            Name = "profiler_capture_frame";
            Description = "Captures the current frame's timing info including deltaTime, FPS, and frame counts.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = "Captured current frame data",
                    ["deltaTime"] = Time.deltaTime,
                    ["smoothDeltaTime"] = Time.smoothDeltaTime,
                    ["fps"] = System.Math.Round(1.0f / Time.smoothDeltaTime, 1),
                    ["frameCount"] = Time.frameCount,
                    ["timeSinceStartup"] = Time.realtimeSinceStartup,
                    ["timeScale"] = Time.timeScale,
                    ["isPlaying"] = Application.isPlaying
                };
            }
            catch (System.Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error capturing frame data: {ex.Message}",
                    "profiler_error"
                );
            }
        }
    }
}
