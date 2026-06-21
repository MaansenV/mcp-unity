using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;
using McpUnity.Resources;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving the hierarchy of a prefab in Prefab Mode.
    /// Reuses GetGameObjectResource.GameObjectToJObject for consistent serialization and size safety.
    /// </summary>
    public class PrefabGetHierarchyTool : McpToolBase
    {
        public PrefabGetHierarchyTool()
        {
            Name = "prefab_get_hierarchy";
            Description = "Retrieves the hierarchy of a prefab currently open in Prefab Mode. Returns the root GameObject and its children with component info. Use 'maxDepth' to control traversal depth. Must have a prefab open in Prefab Mode first.";
        }

        public override JObject Execute(JObject parameters)
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (prefabStage == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "No prefab is currently open in Prefab Mode. Use 'prefab_open' to open a prefab first.",
                    "validation_error"
                );
            }

            GameObject rootGameObject = prefabStage.prefabContentsRoot;

            if (rootGameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Could not find root GameObject in prefab stage.",
                    "not_found_error"
                );
            }

            int maxDepth = parameters?["maxDepth"]?.ToObject<int?>() ?? GetGameObjectResource.DefaultMaxChildDepth;
            maxDepth = Mathf.Clamp(maxDepth, 0, 50);
            bool includeComponents = parameters?["includeComponents"]?.ToObject<bool?>() ?? true;
            bool includeComponentProperties = parameters?["includeComponentProperties"]?.ToObject<bool?>() ?? true;

            // Reuse existing serializer with byte-budget truncation and safety protections
            JObject gameObjectData = GetGameObjectResource.GameObjectToJObject(
                rootGameObject, true, maxDepth, includeComponents, includeComponentProperties);

            string prefabPath = prefabStage.assetPath;

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Retrieved hierarchy for prefab '{rootGameObject.name}'",
                ["prefabPath"] = prefabPath,
                ["rootInstanceId"] = McpObjectId.FromObject(rootGameObject),
                ["gameObject"] = gameObjectData
            };
        }
    }
}
