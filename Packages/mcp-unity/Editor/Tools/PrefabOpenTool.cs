using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for opening a prefab asset in Prefab Mode.
    /// </summary>
    public class PrefabOpenTool : McpToolBase
    {
        public PrefabOpenTool()
        {
            Name = "prefab_open";
            Description = "Opens a prefab asset in Prefab Mode.";
        }

        public override JObject Execute(JObject parameters)
        {
            string prefabPath = parameters?["prefabPath"]?.ToObject<string>();

            if (string.IsNullOrEmpty(prefabPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'prefabPath' not provided",
                    "validation_error"
                );
            }

            prefabPath = prefabPath.Trim().Replace('\\', '/');

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(prefabPath)))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Prefab asset not found at path '{prefabPath}'",
                    "not_found_error"
                );
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Asset at path '{prefabPath}' is not a prefab GameObject",
                    "validation_error"
                );
            }

            AssetDatabase.OpenAsset(prefab);

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Opened prefab asset at '{prefabPath}'",
                ["prefabPath"] = prefabPath
            };
        }
    }
}
