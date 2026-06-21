using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving the current Unity Editor selection.
    /// </summary>
    public class EditorSelectionGetTool : McpToolBase
    {
        public EditorSelectionGetTool()
        {
            Name = "editor_selection_get";
            Description = "Retrieves the current Unity Editor selection details";
        }

        /// <summary>
        /// Execute the EditorSelectionGet tool.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            GameObject activeGameObject = Selection.activeGameObject;
            Object activeObject = Selection.activeObject;

            JArray gameObjects = new JArray(
                Selection.gameObjects.Select(gameObject => new JObject
                {
                    ["instanceId"] = McpObjectId.FromObject(gameObject),
                    ["name"] = gameObject.name,
                    ["path"] = GetHierarchyPath(gameObject.transform)
                })
            );

            JObject activeGameObjectData = activeGameObject != null
                ? new JObject
                {
                    ["instanceId"] = McpObjectId.FromObject(activeGameObject),
                    ["name"] = activeGameObject.name,
                    ["path"] = GetHierarchyPath(activeGameObject.transform)
                }
                : null;

            JObject activeObjectData = activeObject != null
                ? new JObject
                {
                    ["instanceId"] = McpObjectId.FromObject(activeObject),
                    ["name"] = activeObject.name,
                    ["type"] = activeObject.GetType().Name
                }
                : null;

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = "Retrieved Unity Editor selection",
                ["activeGameObject"] = activeGameObjectData,
                ["gameObjects"] = gameObjects,
                ["activeObject"] = activeObjectData,
                ["count"] = Selection.objects.Length
            };
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var pathParts = new System.Collections.Generic.List<string>();
            var current = transform;

            while (current != null)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            pathParts.Reverse();
            return string.Join("/", pathParts);
        }
    }
}
