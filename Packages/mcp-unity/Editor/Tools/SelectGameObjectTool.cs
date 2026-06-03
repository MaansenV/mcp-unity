using System;
using System.Linq;
using System.Threading.Tasks;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for selecting one or more objects in the Unity Editor.
    /// Supports selection by path, name, instance ID, or multiple paths,
    /// with optional additive selection and scene framing.
    /// </summary>
    public class SelectGameObjectTool : McpToolBase
    {
        public SelectGameObjectTool()
        {
            Name = "select_gameobject";
            Description = "Sets the selected object(s) in the Unity editor by path, name, instance ID, or array of paths. Supports additive selection and framing.";
        }
        
        /// <summary>
        /// Execute the SelectGameObject tool with the provided parameters synchronously
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            string objectName = parameters["objectName"]?.ToObject<string>();
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            bool additive = parameters["additive"]?.ToObject<bool>() ?? false;
            bool frame = parameters["frame"]?.ToObject<bool>() ?? false;
            JArray objectPathsArray = parameters["objectPaths"] as JArray;

            var selectedObjects = new System.Collections.Generic.List<UnityEngine.Object>();

            if (objectPathsArray != null)
            {
                foreach (var item in objectPathsArray)
                {
                    var path = item.ToObject<string>();
                    var go = GameObject.Find(path);
                    if (go != null) selectedObjects.Add(go);
                }
            }
            else if (instanceId.HasValue)
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId.Value);
                if (obj != null) selectedObjects.Add(obj);
            }
            else if (!string.IsNullOrEmpty(objectPath))
            {
                var go = GameObject.Find(objectPath);
                if (go != null) selectedObjects.Add(go);
            }
            else if (!string.IsNullOrEmpty(objectName))
            {
                var go = GameObject.Find(objectName);
                if (go != null) selectedObjects.Add(go);
            }
            else
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.InvalidInput("Use 'objectPath', 'objectName', 'instanceId', or 'objectPaths'."),
                    "validation_error"
                );
            }

            if (selectedObjects.Count == 0)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    ToolErrors.NotFound("GameObject"), "not_found"
                );
            }

            if (additive && Selection.objects.Length > 0)
            {
                var combined = new System.Collections.Generic.List<UnityEngine.Object>(Selection.objects);
                combined.AddRange(selectedObjects);
                Selection.objects = combined.ToArray();
            }
            else
            {
                Selection.objects = selectedObjects.ToArray();
            }

            Selection.activeObject = selectedObjects[0];
            EditorGUIUtility.PingObject(selectedObjects[0]);

            if (frame && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            SceneView.RepaintAll();

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Selected {selectedObjects.Count} object(s): {string.Join(", ", selectedObjects.Select(o => o.name))}",
                ["count"] = selectedObjects.Count,
                ["names"] = new JArray(selectedObjects.Select(o => o.name))
            };
        }
    }
}
