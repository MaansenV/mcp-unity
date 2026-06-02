using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for clearing the Unity Editor console.
    /// </summary>
    public class ConsoleClearLogsTool : McpToolBase
    {
        public ConsoleClearLogsTool()
        {
            Name = "console_clear_logs";
            Description = "Clears the Unity Editor console logs by clearing the developer console.";
        }

        /// <summary>
        /// Execute the ConsoleClearLogs tool.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            Debug.ClearDeveloperConsole();

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = "Successfully cleared the Unity Editor console logs"
            };
        }
    }
}
