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
    /// Tool for saving prefab assets or prefab instance overrides.
    /// </summary>
    public class PrefabSaveTool : McpToolBase
    {
        public PrefabSaveTool()
        {
            Name = "prefab_save";
            Description = "Saves a prefab asset directly, or applies prefab instance overrides, or saves a scene object as a prefab asset.";
        }

        public override JObject Execute(JObject parameters)
        {
            int? instanceId = parameters?["instanceId"]?.ToObject<int?>();
            string objectPath = parameters?["objectPath"]?.ToObject<string>();
            string prefabPath = parameters?["prefabPath"]?.ToObject<string>();
            bool applyOverrides = parameters?["applyOverrides"]?.ToObject<bool?>() ?? false;

            GameObject gameObject = null;
            string objectPathUsed = objectPath;

            if (instanceId.HasValue || !string.IsNullOrEmpty(objectPath))
            {
                JObject error = GameObjectToolUtils.FindGameObject(instanceId, objectPath, out gameObject, out objectPathUsed);
                if (error != null)
                {
                    return error;
                }
            }

            if (applyOverrides)
            {
                if (gameObject == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Either 'instanceId' or 'objectPath' must be provided when 'applyOverrides' is true",
                        "validation_error"
                    );
                }

                GameObject prefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
                if (prefabInstanceRoot == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "The specified GameObject is not part of a prefab instance",
                        "validation_error"
                    );
                }

                PrefabUtility.ApplyPrefabInstance(prefabInstanceRoot, InteractionMode.UserAction);

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Applied prefab overrides for '{prefabInstanceRoot.name}'",
                    ["instanceId"] = McpObjectId.FromObject(prefabInstanceRoot),
                    ["objectPath"] = objectPathUsed,
                    ["prefabPath"] = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabInstanceRoot)
                };
            }

            if (!string.IsNullOrEmpty(prefabPath))
            {
                prefabPath = NormalizeAssetPath(prefabPath.Trim());

                if (gameObject != null)
                {
                    if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                    {
                        try
                        {
                            PrefabUtility.SavePrefabAsset(gameObject);
                        }
                        catch (Exception ex)
                        {
                            return McpUnitySocketHandler.CreateErrorResponse(
                                $"Failed to save prefab asset at '{prefabPath}': {ex.Message}",
                                "save_error"
                            );
                        }
                    }
                    else
                    {
                        EnsureFolderHierarchy(Path.GetDirectoryName(prefabPath));

                        PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath, out bool prefabSaved);
                        if (!prefabSaved)
                        {
                            return McpUnitySocketHandler.CreateErrorResponse(
                                $"Failed to save scene object as prefab at '{prefabPath}'",
                                "save_error"
                            );
                        }
                    }
                }
                else
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Prefab asset not found at path '{prefabPath}'",
                            "not_found_error"
                        );
                    }

                    try
                    {
                        PrefabUtility.SavePrefabAsset(prefab);
                    }
                    catch (Exception ex)
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Failed to save prefab asset at '{prefabPath}': {ex.Message}",
                            "save_error"
                        );
                    }
                }

                string guid = AssetDatabase.AssetPathToGUID(prefabPath);

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Successfully saved prefab at '{prefabPath}'",
                    ["prefabPath"] = prefabPath,
                    ["guid"] = guid,
                    ["instanceId"] = gameObject != null ? new JValue(McpObjectId.FromObject(gameObject)) : null,
                    ["objectPath"] = objectPathUsed
                };
            }

            if (gameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Either 'prefabPath' or ('instanceId'/'objectPath') must be provided",
                    "validation_error"
                );
            }

            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                try
                {
                    PrefabUtility.SavePrefabAsset(gameObject);
                }
                catch (Exception ex)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to save prefab asset for '{gameObject.name}': {ex.Message}",
                        "save_error"
                    );
                }

                string assetPath = AssetDatabase.GetAssetPath(gameObject);
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Successfully saved prefab asset '{gameObject.name}'",
                    ["prefabPath"] = assetPath,
                    ["guid"] = AssetDatabase.AssetPathToGUID(assetPath),
                    ["instanceId"] = McpObjectId.FromObject(gameObject),
                    ["objectPath"] = objectPathUsed
                };
            }

            if (!string.IsNullOrEmpty(prefabPath))
            {
                // Handled above.
            }

            return McpUnitySocketHandler.CreateErrorResponse(
                "Parameter 'prefabPath' is required when saving a scene object without applying overrides",
                "validation_error"
            );
        }

        private static void EnsureFolderHierarchy(string folderPath)
        {
            folderPath = NormalizeAssetPath(folderPath);

            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentPath = NormalizeAssetPath(Path.GetDirectoryName(folderPath));
            string folderName = Path.GetFileName(folderPath);

            if (!string.IsNullOrEmpty(parentPath) && !AssetDatabase.IsValidFolder(parentPath))
            {
                EnsureFolderHierarchy(parentPath);
            }

            if (!AssetDatabase.IsValidFolder(folderPath) && !string.IsNullOrEmpty(parentPath) && !string.IsNullOrEmpty(folderName))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }
    }
}
