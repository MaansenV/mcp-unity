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
    /// Tool for finding assets in the Unity AssetDatabase.
    /// </summary>
    public class AssetsFindTool : McpToolBase
    {
        public AssetsFindTool()
        {
            Name = "assets_find";
            Description = "Finds assets in the Unity AssetDatabase using a search filter and optional folder constraints";
        }

        /// <summary>
        /// Execute the AssetsFind tool with the provided parameters.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                string filter = parameters?["filter"]?.ToObject<string>() ?? string.Empty;
                int maxResults = parameters?["maxResults"]?.ToObject<int?>() ?? 10;
                bool includeFolders = parameters?["includeFolders"]?.ToObject<bool?>() ?? false;

                if (maxResults < 1)
                {
                    maxResults = 1;
                }
                else if (maxResults > 200)
                {
                    maxResults = 200;
                }

                string[] searchInFolders = null;
                if (parameters != null && parameters["searchInFolders"] is JArray searchFoldersArray)
                {
                    searchInFolders = new string[searchFoldersArray.Count];
                    for (int i = 0; i < searchFoldersArray.Count; i++)
                    {
                        string folder = searchFoldersArray[i]?.ToObject<string>();
                        if (string.IsNullOrWhiteSpace(folder))
                        {
                            return McpUnitySocketHandler.CreateErrorResponse(
                                $"Invalid folder path at index {i}: value is empty",
                                "validation_error"
                            );
                        }

                        if (!folder.StartsWith("Assets", StringComparison.Ordinal) &&
                            !folder.StartsWith("Packages", StringComparison.Ordinal))
                        {
                            return McpUnitySocketHandler.CreateErrorResponse(
                                $"Invalid folder path '{folder}'. searchInFolders entries must start with 'Assets' or 'Packages'",
                                "validation_error"
                            );
                        }

                        if (!AssetDatabase.IsValidFolder(folder))
                        {
                            return McpUnitySocketHandler.CreateErrorResponse(
                                $"Folder path '{folder}' is not a valid Unity folder",
                                "not_found_error"
                            );
                        }

                        searchInFolders[i] = folder;
                    }
                }

                string[] guids = AssetDatabase.FindAssets(filter, searchInFolders);
                int totalGuidMatches = guids.Length;

                JArray assets = new JArray();
                int returnedCount = 0;
                for (int i = 0; i < guids.Length; i++)
                {
                    string guid = guids[i];
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    bool isFolder = AssetDatabase.IsValidFolder(path);
                    if (isFolder && !includeFolders)
                    {
                        continue;
                    }

                    UnityEngine.Object asset = isFolder
                        ? AssetDatabase.LoadMainAssetAtPath(path)
                        : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

                    string fileName = Path.GetFileName(path);
                    string extension = isFolder ? string.Empty : Path.GetExtension(path);

                    returnedCount++;

                    if (assets.Count < maxResults)
                    {
                        assets.Add(new JObject
                        {
                            ["guid"] = guid,
                            ["path"] = path,
                            ["name"] = asset != null ? asset.name : fileName,
                            ["filename"] = fileName,
                            ["extension"] = extension,
                            ["type"] = isFolder ? "Folder" : (asset != null ? asset.GetType().Name : "Unknown"),
                            ["isFolder"] = isFolder
                        });
                    }
                }

                bool truncated = returnedCount > maxResults;

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Found {assets.Count} asset(s) matching filter '{filter}'",
                    ["assets"] = assets,
                    ["count"] = assets.Count,
                    ["totalGuidMatches"] = totalGuidMatches,
                    ["truncated"] = truncated
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error finding assets: {ex.Message}",
                    "asset_search_error"
                );
            }
        }
    }
}
