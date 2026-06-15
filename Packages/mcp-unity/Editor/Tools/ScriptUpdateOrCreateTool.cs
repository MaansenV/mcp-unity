using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for creating or updating a C# script file in the Unity project.
    /// Writes the content to the file and optionally triggers a recompile.
    /// </summary>
    public class ScriptUpdateOrCreateTool : McpToolBase
    {
        public ScriptUpdateOrCreateTool()
        {
            Name = "script_update_or_create";
            Description = "Creates or updates a C# script file in the Unity project";
            IsAsync = true;
        }

        public override void ExecuteAsync(JObject parameters, System.Threading.Tasks.TaskCompletionSource<JObject> tcs)
        {
            string filePath = parameters["filePath"]?.ToObject<string>();
            string content = parameters["content"]?.ToObject<string>();
            bool recompile = parameters["recompile"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(filePath))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.InvalidInput("filePath is required (relative to project root, e.g. 'Assets/Scripts/MyClass.cs')"),
                    "validation_error"));
                return;
            }

            if (string.IsNullOrEmpty(content))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.InvalidInput("content is required (the C# source code to write)"),
                    "validation_error"));
                return;
            }

            // Normalize path separators
            filePath = filePath.Replace('\\', '/');

            // Ensure the file has a .cs extension
            if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                filePath += ".cs";
            }

            // Resolve to absolute path within the Unity project
            string projectRoot = Application.dataPath.Replace("/Assets", "");
            string absolutePath = Path.Combine(projectRoot, filePath);

            // Security: ensure the path stays within the project
            string fullPath = Path.GetFullPath(absolutePath);
            string projectFullPath = Path.GetFullPath(projectRoot);
            if (!fullPath.StartsWith(projectFullPath))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.InvalidInput("Path is outside the Unity project directory"),
                    "validation_error"));
                return;
            }

            try
            {
                bool fileExists = File.Exists(fullPath);

                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write the file
                File.WriteAllText(fullPath, content);

                // Refresh AssetDatabase so Unity picks up the new/modified file
                AssetDatabase.Refresh();

                string action = fileExists ? "Updated" : "Created";
                string message = $"{action} script file '{filePath}'";

                // Optionally trigger a recompile
                if (recompile)
                {
                    // Force a script recompile by touching a meta file or using CompilationPipeline
                    // The simplest approach is to call EditorApplication.RequestScriptCompilation()
                    // but that's only available in Unity 2019.1+. We'll use AssetDatabase.Refresh which
                    // already handles recompilation when scripts change.
                    message += " (AssetDatabase refreshed, Unity will recompile automatically)";
                }

                tcs.SetResult(new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = message,
                    ["filePath"] = filePath,
                    ["created"] = !fileExists,
                    ["sizeBytes"] = new FileInfo(fullPath).Length
                });
            }
            catch (Exception ex)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.ExecutionError($"Failed to write script: {ex.Message}"), "execution_error"));
            }
        }
    }
}
