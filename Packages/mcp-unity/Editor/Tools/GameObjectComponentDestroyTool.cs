using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for removing a component from a specific GameObject.
    /// </summary>
    public class GameObjectComponentDestroyTool : McpToolBase
    {
        public GameObjectComponentDestroyTool()
        {
            Name = "gameobject_component_destroy";
            Description = "Removes a component from a GameObject by instance ID or hierarchy path, with undo support.";
        }

        public override JObject Execute(JObject parameters)
        {
            int? instanceId = parameters?["instanceId"]?.ToObject<int?>();
            string objectPath = parameters?["objectPath"]?.ToObject<string>();
            string componentName = parameters?["componentName"]?.ToObject<string>();

            if (!instanceId.HasValue && string.IsNullOrEmpty(objectPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Either 'instanceId' or 'objectPath' must be provided",
                    "validation_error"
                );
            }

            if (string.IsNullOrEmpty(componentName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'componentName' not provided",
                    "validation_error"
                );
            }

            GameObject gameObject = FindGameObject(instanceId, objectPath, out string objectPathUsed);
            if (gameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"GameObject with path '{objectPath}' or instance ID {instanceId} not found",
                    "not_found_error"
                );
            }

            Type componentType = FindComponentType(componentName);
            Component component = componentType != null
                ? gameObject.GetComponent(componentType)
                : gameObject.GetComponent(componentName);

            if (component == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Component '{componentName}' not found on GameObject '{gameObject.name}'",
                    "not_found_error"
                );
            }

            if (component is Transform)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Transform components cannot be removed from a GameObject",
                    "validation_error"
                );
            }

            Undo.DestroyObjectImmediate(component);
            EditorUtility.SetDirty(gameObject);

            McpLogger.LogInfo($"Removed component '{component.GetType().Name}' from GameObject '{gameObject.name}'");

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Removed component '{component.GetType().Name}' from GameObject '{gameObject.name}'",
                ["componentName"] = component.GetType().Name,
                ["objectPath"] = objectPathUsed
            };
        }

        private static GameObject FindGameObject(int? instanceId, string objectPath, out string objectPathUsed)
        {
            objectPathUsed = objectPath;

            if (instanceId.HasValue)
            {
                var unityObject = McpObjectId.ToObject(instanceId.Value);
                if (unityObject is GameObject gameObject)
                {
                    objectPathUsed = GetHierarchyPath(gameObject);
                    return gameObject;
                }
            }

            if (!string.IsNullOrEmpty(objectPath))
            {
                return GameObject.Find(objectPath);
            }

            return null;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            string path = gameObject.name;
            Transform current = gameObject.transform.parent;

            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        private static Type FindComponentType(string componentName)
        {
            Type type = Type.GetType(componentName);
            if (type != null && typeof(Component).IsAssignableFrom(type))
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetTypes().FirstOrDefault(t => t.Name == componentName && typeof(Component).IsAssignableFrom(t));
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return null;
        }
    }
}
