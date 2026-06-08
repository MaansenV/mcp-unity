using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for creating folders in the Unity AssetDatabase.
    /// Supports single and batch folder creation.
    /// </summary>
    public class AssetsCreateFolderTool : McpToolBase
    {
        private const int MaxBatchSize = 50;

        public AssetsCreateFolderTool()
        {
            Name = "assets_create_folder";
            Description = "Creates one or more folders in the Unity AssetDatabase";
        }

        public override JObject Execute(JObject parameters)
        {
            JArray folders = parameters?["folders"] as JArray;
            string parentPath = parameters?["parentPath"]?.ToObject<string>();
            string newName = parameters?["newName"]?.ToObject<string>();

            bool hasBatch = folders != null && folders.Count > 0;

            if (!hasBatch && (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(newName)))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Provide either 'parentPath' and 'newName', or a non-empty 'folders' array.",
                    "validation_error"
                );
            }

            if (hasBatch && folders.Count > MaxBatchSize)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Maximum of {MaxBatchSize} folders allowed per batch.",
                    "validation_error"
                );
            }

            JArray results = new JArray();
            int succeeded = 0;
            int failed = 0;

            if (hasBatch)
            {
                for (int i = 0; i < folders.Count; i++)
                {
                    JObject folder = folders[i] as JObject;
                    JObject result = CreateFolderResult(folder?["parentPath"]?.ToObject<string>(), folder?["newName"]?.ToObject<string>());
                    result["index"] = i;
                    results.Add(result);

                    if (result["success"]?.ToObject<bool?>() == true)
                    {
                        succeeded++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }
            else
            {
                JObject result = CreateFolderResult(parentPath, newName);
                results.Add(result);

                if (result["success"]?.ToObject<bool?>() == true)
                {
                    succeeded = 1;
                }
                else
                {
                    failed = 1;
                }
            }

            AssetDatabase.Refresh();

            string message = failed == 0
                ? $"Successfully created {succeeded} folder(s)."
                : $"Created {succeeded} folder(s) with {failed} failure(s).";

            McpLogger.LogInfo($"AssetsCreateFolderTool: {message}");

            return new JObject
            {
                ["success"] = failed == 0,
                ["type"] = "text",
                ["message"] = message,
                ["results"] = results,
                ["summary"] = new JObject
                {
                    ["total"] = results.Count,
                    ["succeeded"] = succeeded,
                    ["failed"] = failed
                }
            };
        }

        private JObject CreateFolderResult(string parentPath, string newName)
        {
            parentPath = parentPath?.Trim();
            newName = newName?.Trim();

            JObject result = new JObject
            {
                ["success"] = false,
                ["path"] = null,
                ["guid"] = null
            };

            if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(newName))
            {
                result["error"] = "Both 'parentPath' and 'newName' are required.";
                return result;
            }

            if (!parentPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                result["error"] = $"Parent path '{parentPath}' must start with 'Assets'.";
                return result;
            }

            if (!AssetDatabase.IsValidFolder(parentPath))
            {
                result["error"] = $"Parent folder '{parentPath}' does not exist.";
                return result;
            }

            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                result["error"] = $"Folder name '{newName}' contains invalid filename characters.";
                return result;
            }

            string targetPath = NormalizeAssetPath($"{parentPath.TrimEnd('/')}/{newName}");

            if (AssetDatabase.IsValidFolder(targetPath) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(targetPath)))
            {
                result["error"] = $"Target folder '{targetPath}' already exists.";
                return result;
            }

            // Group Undo operation to address Undo-System interference mentioned in bug report
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"MCP: Create Folder '{newName}'");

            string guid = AssetDatabase.CreateFolder(parentPath, newName);

            if (string.IsNullOrEmpty(guid))
            {
                result["error"] = $"AssetDatabase.CreateFolder failed for '{targetPath}'. Check console for details (permissions, import conflicts, or Editor state).";
                return result;
            }

            string verifiedPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(verifiedPath) || !AssetDatabase.IsValidFolder(verifiedPath))
            {
                result["error"] = $"Created folder GUID '{guid}' but verification failed for path '{verifiedPath}'.";
                return result;
            }

            result["success"] = true;
            result["path"] = verifiedPath;
            result["guid"] = guid;
            return result;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }
    }
}
