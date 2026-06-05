using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving asset metadata and serialized properties from the AssetDatabase.
    /// </summary>
    public class AssetsGetDataTool : McpToolBase
    {
        private const int DefaultMaxProperties = 100;
        private const int HardMaxProperties = 500;
        private const int MaxStringLength = 1000;

        public AssetsGetDataTool()
        {
            Name = "assets_get_data";
            Description = "Retrieves asset metadata and, optionally, serialized property data for an asset by path or GUID.";
        }

        public override JObject Execute(JObject parameters)
        {
            string assetPath = parameters?["assetPath"]?.ToObject<string>();
            string guid = parameters?["guid"]?.ToObject<string>();
            bool includeSerializedProperties = parameters?["includeSerializedProperties"]?.ToObject<bool?>() ?? true;
            int maxProperties = parameters?["maxProperties"]?.ToObject<int?>() ?? DefaultMaxProperties;
            JArray pathsArray = parameters?["paths"]?.ToObject<JArray>();
            HashSet<string> requestedPaths = pathsArray != null 
                ? new HashSet<string>(pathsArray.Select(p => p.ToString())) 
                : null;

            if (maxProperties < 1)
            {
                maxProperties = DefaultMaxProperties;
            }
            else if (maxProperties > HardMaxProperties)
            {
                maxProperties = HardMaxProperties;
            }

            if (string.IsNullOrEmpty(assetPath) && string.IsNullOrEmpty(guid))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'assetPath' or 'guid' not provided",
                    "validation_error"
                );
            }

            if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid))
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

            if (string.IsNullOrEmpty(guid))
            {
                guid = AssetDatabase.AssetPathToGUID(assetPath);
            }

            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to load asset at path '{assetPath}'",
                    "not_found_error"
                );
            }

            bool isFolder = AssetDatabase.IsValidFolder(assetPath);
            bool isMainAsset = AssetDatabase.IsMainAsset(asset);
            string fileName = Path.GetFileName(assetPath);
            string extension = Path.GetExtension(assetPath);
            string assetBundleName = "";
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer != null)
            {
                assetBundleName = importer.assetBundleName ?? "";
            }

            JObject assetData = new JObject
            {
                ["path"] = assetPath,
                ["guid"] = guid,
                ["name"] = asset.name,
                ["filename"] = fileName,
                ["extension"] = extension,
                ["type"] = asset.GetType().Name,
                ["isFolder"] = isFolder,
                ["isMainAsset"] = isMainAsset,
                ["labels"] = new JArray(AssetDatabase.GetLabels(asset)),
                ["assetBundleName"] = assetBundleName,
            };

            if (includeSerializedProperties && !isFolder)
            {
                assetData["serializedProperties"] = GetSerializedProperties(asset, maxProperties, requestedPaths);
                assetData["serializedPropertyCount"] = assetData["serializedProperties"] is JArray props ? props.Count : 0;
            }

            McpLogger.LogInfo($"Retrieved asset data for '{asset.name}' at '{assetPath}'");

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Retrieved asset data for '{asset.name}'",
                ["asset"] = assetData
            };
        }

        private static JArray GetSerializedProperties(UnityEngine.Object asset, int maxProperties, HashSet<string> requestedPaths = null)
        {
            var serializedObject = new SerializedObject(asset);
            var iterator = serializedObject.GetIterator();
            var allProperties = new List<JObject>();

            // First pass: collect all properties (including nested children of custom classes/structs)
            // NextVisible(true) ensures we enter children of each property
            while (iterator.NextVisible(true))
            {
                allProperties.Add(new JObject
                {
                    ["name"] = iterator.name,
                    ["displayName"] = iterator.displayName,
                    ["path"] = iterator.propertyPath,
                    ["type"] = iterator.propertyType.ToString(),
                    ["value"] = SerializePropertyValue(iterator)
                });
            }

            // Second pass: filter by requested paths if provided
            var filtered = new JArray();
            if (requestedPaths != null && requestedPaths.Count > 0)
            {
                foreach (var prop in allProperties)
                {
                    string propertyPath = prop["path"]?.ToString();
                    if (propertyPath != null)
                    {
                        bool matches = requestedPaths.Any(rp => 
                            propertyPath == rp || propertyPath.StartsWith(rp + "."));
                        if (matches)
                        {
                            filtered.Add(prop);
                            if (filtered.Count >= maxProperties)
                                break;
                        }
                    }
                }
            }
            else
            {
                // No filter - return all up to maxProperties
                foreach (var prop in allProperties.Take(maxProperties))
                {
                    filtered.Add(prop);
                }
            }

            return filtered;
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
                case SerializedPropertyType.ObjectReference:
                    UnityEngine.Object reference = property.objectReferenceValue;
                    return new JObject
                    {
                        ["name"] = reference != null ? reference.name : null,
                        ["type"] = reference != null ? reference.GetType().Name : null,
                        ["instanceId"] = reference != null ? reference.GetInstanceID() : 0
                    };
                default:
                    return new JValue(property.ToString());
            }
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
