using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving the current Unity Editor application state.
    /// </summary>
    public class EditorApplicationGetStateTool : McpToolBase
    {
        public EditorApplicationGetStateTool()
        {
            Name = "editor_application_get_state";
            Description = "Retrieves the current Unity Editor application state and environment details";
        }

        /// <summary>
        /// Execute the EditorApplicationGetState tool.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = "Retrieved Unity Editor application state",
                ["isPlaying"] = EditorApplication.isPlaying,
                ["isPaused"] = EditorApplication.isPaused,
                ["isCompiling"] = EditorApplication.isCompiling,
                ["isUpdating"] = EditorApplication.isUpdating,
                ["isFocused"] = EditorApplication.isFocused,
                ["applicationPath"] = EditorApplication.applicationPath,
                ["unityVersion"] = Application.unityVersion,
                ["platform"] = Application.platform.ToString(),
                ["dataPath"] = Application.dataPath
            };
        }
    }
}
