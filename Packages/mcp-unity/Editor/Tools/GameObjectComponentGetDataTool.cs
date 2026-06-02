using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving serialized data from a component on a GameObject.
    /// </summary>
    public class GameObjectComponentGetDataTool : McpToolBase
    {
        public GameObjectComponentGetDataTool()
        {
            Name = "gameobject_component_get";
            Description = "Gets serialized component data from a GameObject by instance ID or hierarchy path.";
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

            JObject serializedProperties = SerializeComponent(component);

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Retrieved component data for '{component.GetType().Name}' on GameObject '{gameObject.name}'",
                ["componentType"] = component.GetType().FullName,
                ["enabled"] = GetEnabledState(component),
                ["instanceId"] = gameObject.GetInstanceID(),
                ["objectPath"] = objectPathUsed,
                ["componentName"] = component.GetType().Name,
                ["serializedProperties"] = serializedProperties
            };
        }

        private static GameObject FindGameObject(int? instanceId, string objectPath, out string objectPathUsed)
        {
            objectPathUsed = objectPath;

            if (instanceId.HasValue)
            {
                var unityObject = EditorUtility.InstanceIDToObject(instanceId.Value);
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
                    foreach (Type t in assembly.GetTypes())
                    {
                        if (t.Name == componentName && typeof(Component).IsAssignableFrom(t))
                        {
                            return t;
                        }
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return null;
        }

        private static JObject SerializeComponent(Component component)
        {
            var serializedObject = new SerializedObject(component);
            var iterator = serializedObject.GetIterator();
            var properties = new JObject();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                properties[iterator.propertyPath] = new JObject
                {
                    ["name"] = iterator.name,
                    ["displayName"] = iterator.displayName,
                    ["path"] = iterator.propertyPath,
                    ["type"] = iterator.propertyType.ToString(),
                    ["value"] = SerializePropertyValue(iterator)
                };
            }

            return properties;
        }

        private static JToken SerializePropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return new JValue(property.longValue);
                case SerializedPropertyType.Boolean:
                    return new JValue(property.boolValue);
                case SerializedPropertyType.Float:
                    return new JValue(property.doubleValue);
                case SerializedPropertyType.String:
                    return new JValue(property.stringValue);
                case SerializedPropertyType.Color:
                    return new JObject
                    {
                        ["r"] = property.colorValue.r,
                        ["g"] = property.colorValue.g,
                        ["b"] = property.colorValue.b,
                        ["a"] = property.colorValue.a
                    };
                case SerializedPropertyType.ObjectReference:
                    UnityEngine.Object reference = property.objectReferenceValue;
                    return new JObject
                    {
                        ["name"] = reference != null ? reference.name : null,
                        ["type"] = reference != null ? reference.GetType().FullName : null,
                        ["instanceId"] = reference != null ? reference.GetInstanceID() : 0
                    };
                default:
                    return new JValue(property.ToString());
            }
        }

        private static JToken GetEnabledState(Component component)
        {
            if (component is Behaviour behaviour)
            {
                return new JValue(behaviour.enabled);
            }

            if (component is Renderer renderer)
            {
                return new JValue(renderer.enabled);
            }

            return JValue.CreateNull();
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
    }
}
