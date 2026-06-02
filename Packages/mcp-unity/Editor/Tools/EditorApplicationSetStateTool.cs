using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for setting Unity Editor application state.
    /// </summary>
    public class EditorApplicationSetStateTool : McpToolBase
    {
        public EditorApplicationSetStateTool()
        {
            Name = "editor_application_set_state";
            Description = "Sets Unity Editor application play and pause state";
        }

        /// <summary>
        /// Execute the EditorApplicationSetState tool.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            bool hasIsPlaying = parameters != null && parameters["isPlaying"] != null;
            bool hasIsPaused = parameters != null && parameters["isPaused"] != null;

            if (hasIsPlaying)
            {
                EditorApplication.isPlaying = parameters["isPlaying"].ToObject<bool>();
            }

            if (hasIsPaused)
            {
                EditorApplication.isPaused = parameters["isPaused"].ToObject<bool>();
            }

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = "Successfully updated Unity Editor application state",
                ["isPlaying"] = EditorApplication.isPlaying,
                ["isPaused"] = EditorApplication.isPaused,
                ["isCompiling"] = EditorApplication.isCompiling,
                ["isUpdating"] = EditorApplication.isUpdating,
                ["isFocused"] = EditorApplication.isFocused
            };
        }
    }
}
