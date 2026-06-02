using System;
using System.Collections.Generic;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for deleting assets from the Unity AssetDatabase.
    /// </summary>
    public class AssetsDeleteTool : McpToolBase
    {
        public AssetsDeleteTool()
        {
            Name = "assets_delete";
            Description = "Deletes one or more assets from the Unity AssetDatabase";
        }

        /// <summary>
        /// Execute the AssetsDelete tool with the provided parameters.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                bool confirmDelete = parameters?["confirmDelete"]?.ToObject<bool?>() ?? false;
                if (!confirmDelete)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Deletion requires confirmDelete to be true",
                        "validation_error"
                    );
                }

                List<string> deletePaths = new List<string>();
                JArray pathsArray = parameters?["paths"] as JArray;

                if (pathsArray != null)
                {
                    for (int i = 0; i < pathsArray.Count; i++)
                    {
                        string path = NormalizeAssetPath(pathsArray[i]?.ToObject<string>());
                        if (string.IsNullOrEmpty(path))
                        {
                            return McpUnitySocketHandler.CreateErrorResponse(
                                $"Invalid path at index {i}: value is empty",
                                "validation_error"
                            );
                        }

                        deletePaths.Add(path);
                    }
                }
                else
                {
                    string singlePath = NormalizeAssetPath(parameters?["path"]?.ToObject<string>());
                    if (!string.IsNullOrEmpty(singlePath))
                    {
                        deletePaths.Add(singlePath);
                    }
                }

                if (deletePaths.Count == 0)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Provide either 'path' or 'paths'",
                        "validation_error"
                    );
                }

                if (deletePaths.Count > 50)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Maximum batch size is 50. Received {deletePaths.Count} paths.",
                        "validation_error"
                    );
                }

                for (int i = 0; i < deletePaths.Count; i++)
                {
                    string path = deletePaths[i];

                    if (!IsUnderAssets(path))
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Path '{path}' must be under 'Assets'",
                            "validation_error"
                        );
                    }

                    if (!AssetExists(path))
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Path '{path}' does not exist",
                            "not_found_error"
                        );
                    }
                }

                int succeeded = 0;
                JArray failed = new JArray();
                JArray deleted = new JArray();

                for (int i = 0; i < deletePaths.Count; i++)
                {
                    string path = deletePaths[i];
                    bool wasDeleted = AssetDatabase.DeleteAsset(path);
                    if (wasDeleted)
                    {
                        succeeded++;
                        deleted.Add(path);
                    }
                    else
                    {
                        failed.Add(path);
                    }
                }

                AssetDatabase.Refresh();

                return new JObject
                {
                    ["success"] = failed.Count == 0,
                    ["type"] = "text",
                    ["message"] = $"Deleted {deleted.Count} asset(s); {failed.Count} failed",
                    ["deleted"] = deleted,
                    ["failed"] = failed,
                    ["summary"] = new JObject
                    {
                        ["total"] = deletePaths.Count,
                        ["deleted"] = deleted.Count,
                        ["failed"] = failed.Count
                    }
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error deleting assets: {ex.Message}",
                    "asset_delete_error"
                );
            }
        }

        private bool IsUnderAssets(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets", StringComparison.Ordinal);
        }

        private bool AssetExists(string path)
        {
            return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
        }

        private string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }
    }
}
