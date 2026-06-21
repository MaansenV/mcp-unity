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
    /// Tool for creating a prefab asset from a scene GameObject.
    /// </summary>
    public class PrefabCreateFromSceneTool : McpToolBase
    {
        public PrefabCreateFromSceneTool()
        {
            Name = "prefab_create_from_scene";
            Description = "Creates a prefab asset from a scene GameObject by instance ID or hierarchy path.";
        }

        public override JObject Execute(JObject parameters)
        {
            int? instanceId = parameters?["instanceId"]?.ToObject<int?>();
            string objectPath = parameters?["objectPath"]?.ToObject<string>();
            string prefabPath = parameters?["prefabPath"]?.ToObject<string>();

            if (!instanceId.HasValue && string.IsNullOrEmpty(objectPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Either 'instanceId' or 'objectPath' must be provided",
                    "validation_error"
                );
            }

            if (string.IsNullOrEmpty(prefabPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'prefabPath' not provided",
                    "validation_error"
                );
            }

            prefabPath = NormalizeAssetPath(prefabPath.Trim());

            if (!prefabPath.StartsWith("Assets", StringComparison.Ordinal) ||
                !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Parameter 'prefabPath' must start with 'Assets' and end with '.prefab'",
                    "validation_error"
                );
            }

            JObject error = GameObjectToolUtils.FindGameObject(instanceId, objectPath, out GameObject gameObject, out _);
            if (error != null)
            {
                return error;
            }

            EnsureFolderHierarchy(Path.GetDirectoryName(prefabPath));

            PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath, out bool prefabSaved);
            if (!prefabSaved)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to create prefab asset at '{prefabPath}'",
                    "prefab_save_error"
                );
            }

            string guid = AssetDatabase.AssetPathToGUID(prefabPath);

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully created prefab asset at '{prefabPath}'",
                ["prefabPath"] = prefabPath,
                ["guid"] = guid,
                ["instanceId"] = McpObjectId.FromObject(gameObject)
            };
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
