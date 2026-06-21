using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving metadata and serialized data for any UnityEngine.Object by instance ID.
    /// </summary>
    public class ObjectGetDataTool : McpToolBase
    {
        private const int DefaultMaxProperties = 100;
        private const int HardMaxProperties = 500;
        private const int MaxStringLength = 1000;

        public ObjectGetDataTool()
        {
            Name = "object_get_data";
            Description = "Gets metadata and optional serialized data for any UnityEngine.Object by instance ID.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                int? instanceId = parameters?["instanceId"]?.ToObject<int?>();
                bool includeSerializedProperties = parameters?["includeSerializedProperties"]?.ToObject<bool?>() ?? true;
                int maxProperties = parameters?["maxProperties"]?.ToObject<int?>() ?? DefaultMaxProperties;

                if (!instanceId.HasValue)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Required parameter 'instanceId' not provided",
                        "validation_error"
                    );
                }

                if (maxProperties < 1)
                {
                    maxProperties = DefaultMaxProperties;
                }
                else if (maxProperties > HardMaxProperties)
                {
                    maxProperties = HardMaxProperties;
                }

                UnityEngine.Object unityObject = McpObjectId.ToObject(instanceId.Value);
                if (unityObject == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Object with instance ID {instanceId.Value} not found",
                        "not_found_error"
                    );
                }

                string assetPath = AssetDatabase.Contains(unityObject) ? AssetDatabase.GetAssetPath(unityObject) : null;
                bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(unityObject) || PrefabUtility.IsPartOfPrefabInstance(unityObject);

                JObject objectData = new JObject
                {
                    ["instanceId"] = instanceId.Value,
                    ["name"] = unityObject.name,
                    ["type"] = unityObject.GetType().Name,
                    ["isPrefab"] = isPrefab,
                    ["assetPath"] = assetPath
                };

                if (includeSerializedProperties)
                {
                    JArray serializedProperties = GetSerializedProperties(unityObject, maxProperties);
                    objectData["serializedProperties"] = serializedProperties;
                    objectData["serializedPropertyCount"] = serializedProperties.Count;
                }

                McpLogger.LogInfo($"Retrieved object data for '{unityObject.name}' (instance ID {instanceId.Value})");

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Retrieved object data for '{unityObject.name}'",
                    ["object"] = objectData
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error retrieving object data: {ex.Message}",
                    "object_get_data_error"
                );
            }
        }

        private static JArray GetSerializedProperties(UnityEngine.Object unityObject, int maxProperties)
        {
            var serializedObject = new SerializedObject(unityObject);
            serializedObject.Update();

            var iterator = serializedObject.GetIterator();
            var properties = new JArray();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (properties.Count >= maxProperties)
                {
                    break;
                }

                properties.Add(new JObject
                {
                    ["name"] = iterator.name,
                    ["displayName"] = iterator.displayName,
                    ["path"] = iterator.propertyPath,
                    ["type"] = iterator.propertyType.ToString(),
                    ["value"] = SerializePropertyValue(iterator)
                });
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
                    return new JValue(TruncateString(property.stringValue));
                case SerializedPropertyType.Color:
                    return new JObject
                    {
                        ["r"] = property.colorValue.r,
                        ["g"] = property.colorValue.g,
                        ["b"] = property.colorValue.b,
                        ["a"] = property.colorValue.a
                    };
                case SerializedPropertyType.Vector2:
                    return new JObject
                    {
                        ["x"] = property.vector2Value.x,
                        ["y"] = property.vector2Value.y
                    };
                case SerializedPropertyType.Vector3:
                    return new JObject
                    {
                        ["x"] = property.vector3Value.x,
                        ["y"] = property.vector3Value.y,
                        ["z"] = property.vector3Value.z
                    };
                case SerializedPropertyType.ObjectReference:
                {
                    UnityEngine.Object reference = property.objectReferenceValue;
                    return new JObject
                    {
                        ["name"] = reference != null ? reference.name : null,
                        ["type"] = reference != null ? reference.GetType().Name : null,
                        ["instanceId"] = reference != null ? McpObjectId.FromObject(reference) : 0
                    };
                }
                case SerializedPropertyType.Enum:
                    return new JValue(GetEnumName(property));
                default:
                    return new JValue(property.ToString());
            }
        }

        private static string GetEnumName(SerializedProperty property)
        {
            int index = property.enumValueIndex;
            if (index >= 0 && index < property.enumNames.Length)
            {
                return property.enumNames[index];
            }

            return property.enumValueIndex.ToString();
        }

        private static string TruncateString(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= MaxStringLength)
            {
                return value;
            }

            return value.Substring(0, MaxStringLength);
        }
    }
}
