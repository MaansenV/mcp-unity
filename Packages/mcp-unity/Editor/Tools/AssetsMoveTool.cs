using System;
using System.IO;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for moving assets within the Unity AssetDatabase.
    /// </summary>
    public class AssetsMoveTool : McpToolBase
    {
        public AssetsMoveTool()
        {
            Name = "assets_move";
            Description = "Moves one or more assets within the Unity AssetDatabase";
        }

        /// <summary>
        /// Execute the AssetsMove tool with the provided parameters.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                JArray movesArray = parameters?["moves"] as JArray;

                if (movesArray != null)
                {
                    if (movesArray.Count > 50)
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            "The 'moves' array cannot contain more than 50 items",
                            "validation_error"
                        );
                    }

                    JArray results = new JArray();
                    int succeeded = 0;
                    int failed = 0;

                    for (int i = 0; i < movesArray.Count; i++)
                    {
                        JObject move = movesArray[i] as JObject;
                        if (move == null)
                        {
                            results.Add(new JObject
                            {
                                ["success"] = false,
                                ["error"] = $"Invalid move entry at index {i}",
                                ["srcPath"] = string.Empty,
                                ["destPath"] = string.Empty,
                                ["guid"] = string.Empty
                            });
                            failed++;
                            continue;
                        }

                        string srcPath = NormalizeAssetPath(move["srcPath"]?.ToObject<string>());
                        string destPath = NormalizeAssetPath(move["destPath"]?.ToObject<string>());
                        JObject itemResult = MoveSingleAsset(srcPath, destPath);
                        results.Add(itemResult);

                        if (itemResult["success"]?.ToObject<bool>() == true)
                        {
                            succeeded++;
                        }
                        else
                        {
                            failed++;
                        }
                    }

                    return BuildBatchResponse(results, succeeded, failed);
                }

                string singleSrcPath = NormalizeAssetPath(parameters?["srcPath"]?.ToObject<string>());
                string singleDestPath = NormalizeAssetPath(parameters?["destPath"]?.ToObject<string>());

                if (string.IsNullOrEmpty(singleSrcPath) || string.IsNullOrEmpty(singleDestPath))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Provide either 'srcPath' and 'destPath' or a 'moves' array",
                        "validation_error"
                    );
                }

                JObject singleResult = MoveSingleAsset(singleSrcPath, singleDestPath);
                bool singleSuccess = singleResult["success"]?.ToObject<bool>() == true;

                return new JObject
                {
                    ["success"] = singleSuccess,
                    ["type"] = "text",
                    ["message"] = singleSuccess
                        ? $"Successfully moved asset from '{singleSrcPath}' to '{singleDestPath}'"
                        : singleResult["error"]?.ToObject<string>() ?? $"Failed to move asset from '{singleSrcPath}' to '{singleDestPath}'",
                    ["results"] = new JArray { singleResult },
                    ["summary"] = new JObject
                    {
                        ["total"] = 1,
                        ["succeeded"] = singleSuccess ? 1 : 0,
                        ["failed"] = singleSuccess ? 0 : 1
                    }
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error moving assets: {ex.Message}",
                    "asset_move_error"
                );
            }
        }

        private JObject MoveSingleAsset(string srcPath, string destPath)
        {
            string guid = string.IsNullOrEmpty(srcPath) ? string.Empty : AssetDatabase.AssetPathToGUID(srcPath);

            string validationError = ValidateMovePaths(srcPath, destPath);
            if (!string.IsNullOrEmpty(validationError))
            {
                return new JObject
                {
                    ["success"] = false,
                    ["srcPath"] = srcPath ?? string.Empty,
                    ["destPath"] = destPath ?? string.Empty,
                    ["guid"] = guid,
                    ["error"] = validationError
                };
            }

            string moveError = AssetDatabase.MoveAsset(srcPath, destPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                return new JObject
                {
                    ["success"] = false,
                    ["srcPath"] = srcPath,
                    ["destPath"] = destPath,
                    ["guid"] = guid,
                    ["error"] = moveError
                };
            }

            return new JObject
            {
                ["success"] = true,
                ["srcPath"] = srcPath,
                ["destPath"] = destPath,
                ["guid"] = guid
            };
        }

        private string ValidateMovePaths(string srcPath, string destPath)
        {
            if (string.IsNullOrEmpty(srcPath) || string.IsNullOrEmpty(destPath))
            {
                return "Both 'srcPath' and 'destPath' are required";
            }

            if (!IsUnderAssets(srcPath))
            {
                return $"Source path '{srcPath}' must be under 'Assets'";
            }

            if (!IsUnderAssets(destPath))
            {
                return $"Destination path '{destPath}' must be under 'Assets'";
            }

            if (!AssetExists(srcPath))
            {
                return $"Source path '{srcPath}' does not exist";
            }

            if (AssetExists(destPath) || AssetDatabase.IsValidFolder(destPath))
            {
                return $"Destination path '{destPath}' already exists";
            }

            string destFolder = Path.GetDirectoryName(destPath);
            if (string.IsNullOrEmpty(destFolder))
            {
                return $"Destination path '{destPath}' must include a valid folder";
            }

            destFolder = NormalizeAssetPath(destFolder);
            if (!AssetDatabase.IsValidFolder(destFolder))
            {
                return $"Destination folder '{destFolder}' does not exist";
            }

            return string.Empty;
        }

        private bool AssetExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
        }

        private bool IsUnderAssets(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets", StringComparison.Ordinal);
        }

        private string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }

        private JObject BuildBatchResponse(JArray results, int succeeded, int failed)
        {
            int total = succeeded + failed;
            return new JObject
            {
                ["success"] = failed == 0,
                ["type"] = "text",
                ["message"] = $"Moved {succeeded} of {total} asset(s); {failed} failed",
                ["results"] = results,
                ["summary"] = new JObject
                {
                    ["total"] = total,
                    ["succeeded"] = succeeded,
                    ["failed"] = failed
                }
            };
        }
    }
}
