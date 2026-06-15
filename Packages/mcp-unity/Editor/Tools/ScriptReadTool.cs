using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for reading a C# script file from the Unity project.
    /// Returns the file content and metadata.
    /// </summary>
    public class ScriptReadTool : McpToolBase
    {
        public ScriptReadTool()
        {
            Name = "script_read";
            Description = "Reads a C# script file from the Unity project and returns its content";
        }

        public override JObject Execute(JObject parameters)
        {
            string filePath = parameters["filePath"]?.ToObject<string>();

            if (string.IsNullOrEmpty(filePath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.InvalidInput("filePath is required (relative to project root, e.g. 'Assets/Scripts/MyClass.cs')"),
                    "validation_error");
            }

            // Normalize path separators
            filePath = filePath.Replace('\\', '/');

            // Resolve to absolute path within the Unity project
            string projectRoot = Application.dataPath.Replace("/Assets", "");
            string absolutePath = Path.Combine(projectRoot, filePath);

            // Security: ensure the path stays within the project
            string fullPath = Path.GetFullPath(absolutePath);
            string projectFullPath = Path.GetFullPath(projectRoot);
            if (!fullPath.StartsWith(projectFullPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.InvalidInput("Path is outside the Unity project directory"),
                    "validation_error");
            }

            if (!File.Exists(fullPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.NotFound($"Script file '{filePath}'"), "not_found");
            }

            try
            {
                string content = File.ReadAllText(fullPath);
                var fileInfo = new FileInfo(fullPath);

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Read script file '{filePath}'",
                    ["filePath"] = filePath,
                    ["content"] = content,
                    ["sizeBytes"] = fileInfo.Length,
                    ["lastModified"] = fileInfo.LastWriteTimeUtc.ToString("o")
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.ExecutionError("read script", ex.Message), "execution_error");
            }
        }
    }
}
