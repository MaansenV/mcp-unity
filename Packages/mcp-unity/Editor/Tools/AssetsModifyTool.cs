using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for modifying serialized properties of a Unity asset.
    /// </summary>
    public class AssetsModifyTool : McpToolBase
    {
        public AssetsModifyTool()
        {
            Name = "assets_modify";
            Description = "Modifies serialized properties of a Unity asset. Blocks modification of built-in and Packages/ assets.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string assetPath = parameters?["assetPath"]?.ToObject<string>();
                string guid = parameters?["guid"]?.ToObject<string>();
                JObject properties = parameters?["properties"] as JObject;

                if (string.IsNullOrWhiteSpace(assetPath) && string.IsNullOrWhiteSpace(guid))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Required parameter 'assetPath' or 'guid' not provided",
                        "validation_error"
                    );
                }

                if (properties == null || !properties.Properties().Any())
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Required parameter 'properties' not provided or empty",
                        "validation_error"
                    );
                }

                if (string.IsNullOrWhiteSpace(assetPath) && !string.IsNullOrWhiteSpace(guid))
                {
                    assetPath = AssetDatabase.GUIDToAssetPath(guid);

                    if (string.IsNullOrEmpty(assetPath))
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Asset with GUID '{guid}' not found",
                            "not_found_error"
                        );
                    }
                }

                if (string.IsNullOrWhiteSpace(guid))
                {
                    guid = AssetDatabase.AssetPathToGUID(assetPath);
                }

                if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Modification of package assets is not allowed: '{assetPath}'",
                        "permission_error"
                    );
                }

                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to load asset at path '{assetPath}'",
                        "not_found_error"
                    );
                }

                string loadedAssetPath = AssetDatabase.GetAssetPath(asset);
                if (IsBuiltInAsset(assetPath, loadedAssetPath, asset))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Modification of built-in assets is not allowed: '{assetPath}'",
                        "permission_error"
                    );
                }

                var serializedObject = new SerializedObject(asset);
                serializedObject.Update();

                var modifiedProperties = new JArray();

                foreach (JProperty propertyEntry in properties.Properties())
                {
                    SerializedProperty serializedProperty = serializedObject.FindProperty(propertyEntry.Name);
                    if (serializedProperty == null)
                    {
                        continue;
                    }

                    if (!TrySetSerializedPropertyValue(serializedProperty, propertyEntry.Value))
                    {
                        continue;
                    }

                    modifiedProperties.Add(propertyEntry.Name);
                }

                serializedObject.ApplyModifiedProperties();

                int requestedCount = properties.Properties().Count();
                int modifiedCount = modifiedProperties.Count;
                int skippedCount = requestedCount - modifiedCount;

                McpLogger.LogInfo($"Modified {modifiedCount}/{requestedCount} serialized propertie(s) on '{assetPath}'");

                if (modifiedCount == 0 && requestedCount > 0)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["type"] = "text",
                        ["message"] = $"No properties were modified. {skippedCount} property path(s) were not found or unsupported.",
                        ["modifiedCount"] = 0,
                        ["requestedCount"] = requestedCount,
                        ["skippedCount"] = skippedCount,
                        ["modifiedProperties"] = modifiedProperties,
                        ["path"] = assetPath,
                        ["guid"] = guid,
                        ["errorType"] = "no_changes"
                    };
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Modified {modifiedCount}/{requestedCount} serialized propertie(s) on '{assetPath}'",
                    ["modifiedCount"] = modifiedCount,
                    ["requestedCount"] = requestedCount,
                    ["skippedCount"] = skippedCount,
                    ["modifiedProperties"] = modifiedProperties,
                    ["path"] = assetPath,
                    ["guid"] = guid
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error modifying asset: {ex.Message}",
                    "asset_modify_error"
                );
            }
        }

        private static bool IsBuiltInAsset(string requestedPath, string loadedPath, UnityEngine.Object asset)
        {
            if (!string.IsNullOrEmpty(requestedPath) &&
                (requestedPath.StartsWith("Resources/unity_builtin_extra", StringComparison.Ordinal) ||
                 requestedPath.StartsWith("Library/unity default resources", StringComparison.Ordinal)))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(loadedPath) &&
                (loadedPath.StartsWith("Resources/unity_builtin_extra", StringComparison.Ordinal) ||
                 loadedPath.StartsWith("Library/unity default resources", StringComparison.Ordinal)))
            {
                return true;
            }

            return asset != null && !AssetDatabase.Contains(asset);
        }

        private static bool TrySetSerializedPropertyValue(SerializedProperty serializedProperty, JToken valueToken)
        {
            switch (serializedProperty.propertyType)
            {
                case SerializedPropertyType.Integer:
                    serializedProperty.longValue = valueToken.ToObject<long>();
                    return true;
                case SerializedPropertyType.Boolean:
                    serializedProperty.boolValue = valueToken.ToObject<bool>();
                    return true;
                case SerializedPropertyType.Float:
                    serializedProperty.floatValue = valueToken.ToObject<float>();
                    return true;
                case SerializedPropertyType.String:
                    serializedProperty.stringValue = valueToken.Type == JTokenType.Null
                        ? string.Empty
                        : valueToken.ToObject<string>();
                    return true;
                case SerializedPropertyType.Color:
                    if (!TryParseColor(valueToken, out Color color))
                    {
                        return false;
                    }

                    serializedProperty.colorValue = color;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseColor(JToken valueToken, out Color color)
        {
            color = default;

            if (valueToken is not JObject colorObject)
            {
                return false;
            }

            if (!TryGetFloat(colorObject, "r", out float r) ||
                !TryGetFloat(colorObject, "g", out float g) ||
                !TryGetFloat(colorObject, "b", out float b))
            {
                return false;
            }

            float a = 1f;
            if (colorObject.TryGetValue("a", StringComparison.OrdinalIgnoreCase, out JToken alphaToken))
            {
                if (!TryConvertToFloat(alphaToken, out a))
                {
                    return false;
                }
            }

            color = new Color(r, g, b, a);
            return true;
        }

        private static bool TryGetFloat(JObject obj, string propertyName, out float value)
        {
            value = 0f;
            if (!obj.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out JToken token))
            {
                return false;
            }

            return TryConvertToFloat(token, out value);
        }

        private static bool TryConvertToFloat(JToken token, out float value)
        {
            value = 0f;

            try
            {
                value = token.ToObject<float>();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
