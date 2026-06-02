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
    /// Tool for copying assets within the AssetDatabase.
    /// Supports single and batch copy operations.
    /// </summary>
    public class AssetsCopyTool : McpToolBase
    {
        private const int MaxBatchSize = 50;

        public AssetsCopyTool()
        {
            Name = "assets_copy";
            Description = "Copies one or more assets within the Unity AssetDatabase";
        }

        public override JObject Execute(JObject parameters)
        {
            JArray copies = parameters?["copies"] as JArray;
            string srcPath = parameters?["srcPath"]?.ToObject<string>();
            string destPath = parameters?["destPath"]?.ToObject<string>();

            bool hasBatch = copies != null && copies.Count > 0;

            if (!hasBatch && (string.IsNullOrWhiteSpace(srcPath) || string.IsNullOrWhiteSpace(destPath)))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Provide either 'srcPath' and 'destPath', or a non-empty 'copies' array.",
                    "validation_error"
                );
            }

            if (hasBatch && copies.Count > MaxBatchSize)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Maximum of {MaxBatchSize} copies allowed per batch.",
                    "validation_error"
                );
            }

            JArray results = new JArray();
            int succeeded = 0;
            int failed = 0;

            if (hasBatch)
            {
                for (int i = 0; i < copies.Count; i++)
                {
                    JObject copy = copies[i] as JObject;
                    JObject result = CopyAssetResult(copy?["srcPath"]?.ToObject<string>(), copy?["destPath"]?.ToObject<string>());
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
                JObject result = CopyAssetResult(srcPath, destPath);
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
                ? $"Successfully copied {succeeded} asset(s)."
                : $"Copied {succeeded} asset(s) with {failed} failure(s).";

            McpLogger.LogInfo($"AssetsCopyTool: {message}");

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

        private JObject CopyAssetResult(string srcPath, string destPath)
        {
            srcPath = NormalizeAssetPath(srcPath?.Trim());
            destPath = NormalizeAssetPath(destPath?.Trim());

            JObject result = new JObject
            {
                ["success"] = false,
                ["srcPath"] = srcPath,
                ["destPath"] = destPath,
                ["guid"] = null
            };

            if (string.IsNullOrWhiteSpace(srcPath) || string.IsNullOrWhiteSpace(destPath))
            {
                result["error"] = "Both 'srcPath' and 'destPath' are required.";
                return result;
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(srcPath)) && !AssetDatabase.IsValidFolder(srcPath))
            {
                result["error"] = $"Source asset '{srcPath}' does not exist.";
                return result;
            }

            if (!destPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                result["error"] = $"Destination path '{destPath}' must be under 'Assets'.";
                return result;
            }

            string destFolder = GetAssetFolderPath(destPath);
            if (string.IsNullOrEmpty(destFolder) || !AssetDatabase.IsValidFolder(destFolder))
            {
                result["error"] = $"Destination folder '{destFolder}' does not exist.";
                return result;
            }

            if (AssetDatabase.IsValidFolder(destPath) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destPath)))
            {
                result["error"] = $"Destination path '{destPath}' already exists.";
                return result;
            }

            bool copied = AssetDatabase.CopyAsset(srcPath, destPath);
            AssetDatabase.Refresh();

            if (!copied)
            {
                result["error"] = $"Failed to copy asset from '{srcPath}' to '{destPath}'.";
                return result;
            }

            result["success"] = true;
            result["guid"] = AssetDatabase.AssetPathToGUID(destPath);
            return result;
        }

        private static string GetAssetFolderPath(string assetPath)
        {
            string normalizedPath = NormalizeAssetPath(assetPath);
            int lastSlash = normalizedPath.LastIndexOf('/');

            if (lastSlash <= 0)
            {
                return null;
            }

            return normalizedPath.Substring(0, lastSlash);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }
    }
}
