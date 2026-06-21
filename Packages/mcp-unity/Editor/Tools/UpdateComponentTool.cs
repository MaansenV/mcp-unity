using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for updating component data in the Unity Editor
    /// </summary>
    public class UpdateComponentTool : McpToolBase
    {
        public UpdateComponentTool()
        {
            Name = "update_component";
            Description = "Updates component fields on a GameObject or adds it to the GameObject if it does not contain the component";
        }
        
        /// <summary>
        /// Execute the UpdateComponent tool with the provided parameters synchronously
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            // Extract parameters
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            string componentName = parameters["componentName"]?.ToObject<string>();
            JObject componentData = parameters["componentData"] as JObject;
            
            // Validate parameters - require either instanceId or objectPath
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
            
            // Find the GameObject by instance ID or path
            GameObject gameObject = null;
            string identifier = "unknown";
            
            if (instanceId.HasValue)
            {
                gameObject = McpObjectId.ToObject(instanceId.Value) as GameObject;
                identifier = $"ID {instanceId.Value}";
            }
            else
            {
                // Find by path
                gameObject = GameObject.Find(objectPath);
                identifier = $"path '{objectPath}'";
                
                if (gameObject == null)
                {
                    // Try to find using the Unity Scene hierarchy path
                    gameObject = FindGameObjectByPath(objectPath);
                }
            }
                    
            if (gameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"GameObject with path '{objectPath}' or instance ID {instanceId} not found", 
                    "not_found_error"
                );
            }
            
            McpLogger.LogInfo($"[MCP Unity] Updating component '{componentName}' on GameObject '{gameObject.name}' (found by {identifier})");
            
            // Try to find the component by name
            Component component = gameObject.GetComponent(componentName);
            
            // If component not found, try to add it
            if (component == null)
            {
                Type componentType = FindComponentType(componentName);
                if (componentType == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Component type '{componentName}' not found in Unity", 
                        "component_error"
                    );
                }
                
                component = Undo.AddComponent(gameObject, componentType);
                
                McpLogger.LogInfo($"[MCP Unity] Added component '{componentName}' to GameObject '{gameObject.name}'");
            }
            // Update component fields
            if (componentData != null && componentData.Count > 0)
            {
                bool success = UpdateComponentData(component, componentData, out string errorMessage);
                // If update failed, return error
                if (!success)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(errorMessage, "update_error");
                }
            }

            // Ensure changes persist (critical for Prefab assets - fixes YAML not updating on disk)
            EnsureChangesSaved(gameObject, component);

            // Create the response
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully updated component '{componentName}' on GameObject '{gameObject.name}'"
            };
        }
        
        /// <summary>
        /// Find a GameObject by its hierarchy path
        /// </summary>
        /// <param name="path">The path to the GameObject (e.g. "Canvas/Panel/Button")</param>
        /// <returns>The GameObject if found, null otherwise</returns>
        private GameObject FindGameObjectByPath(string path)
        {
            // Split the path by '/'
            string[] pathParts = path.Split('/');
            
            // If the path is empty, return null
            if (pathParts.Length == 0)
            {
                return null;
            }
            
            // Search through all root GameObjects in all loaded scenes
            foreach (var scene in UnityEngine.SceneManagement.SceneManager.GetAllScenes())
            {
                if (!scene.IsValid()) continue;
                GameObject[] rootGameObjects = scene.GetRootGameObjects();
                foreach (GameObject rootObj in rootGameObjects)
                {
                    if (rootObj.name == pathParts[0])
                    {
                        // Found the root object, now traverse down the path
                        GameObject current = rootObj;
                        
                        // Start from index 1 since we've already matched the root
                        for (int i = 1; i < pathParts.Length; i++)
                        {
                            Transform child = current.transform.Find(pathParts[i]);
                            if (child == null)
                            {
                                // Path segment not found
                                goto nextRoot;
                            }
                            
                            // Move to the next level
                            current = child.gameObject;
                        }
                        
                        // If we got here, we found the full path
                        return current;
                    }
                    nextRoot:;
                }
            }
            
            // Not found
            return null;
        }
        
        /// <summary>
        /// Find a component type by name
        /// </summary>
        /// <param name="componentName">The name of the component type</param>
        /// <returns>The component type, or null if not found</returns>
        private Type FindComponentType(string componentName)
        {
            // First try direct match
            Type type = Type.GetType(componentName);
            if (type != null && typeof(Component).IsAssignableFrom(type))
            {
                return type;
            }
            
            // Try common Unity namespaces
            string[] commonNamespaces = new string[] 
            {
                "UnityEngine",
                "UnityEngine.UI",
                "UnityEngine.EventSystems",
                "UnityEngine.Animations",
                "UnityEngine.Rendering",
                "TMPro"
            };
            
            foreach (string ns in commonNamespaces)
            {
                type = Type.GetType($"{ns}.{componentName}, UnityEngine");
                if (type != null && typeof(Component).IsAssignableFrom(type))
                {
                    return type;
                }
            }
            
            // Try assemblies search
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
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
                    // Some assemblies might throw exceptions when getting types
                    continue;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Update component data based on the provided JObject
        /// </summary>
        /// <param name="component">The component to update</param>
        /// <param name="componentData">The data to apply to the component</param>
        /// <returns>True if the component was updated successfully</returns>
        private bool UpdateComponentData(Component component, JObject componentData, out string errorMessage)
        {
            errorMessage = "";
            
            if (component == null || componentData == null)
            {
                errorMessage = "Component or component data is null";
                return false;
            }

            Type componentType = component.GetType();
            bool fullSuccess = true;

            // Record object for undo
            Undo.RecordObject(component, $"Update {componentType.Name} fields");
            
            // Process each field or property in the component data
            foreach (var property in componentData.Properties())
            {
                string fieldName = property.Name;
                JToken fieldValue = property.Value;
                
                // Skip null values
                if (string.IsNullOrEmpty(fieldName) || fieldValue.Type == JTokenType.Null)
                {
                    continue;
                }
                
                // Try to update field
                FieldInfo fieldInfo = componentType.GetField(fieldName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                if (fieldInfo != null)
                {
                    // Reject SerializeReference fields — they need concrete type metadata
                    if (fieldInfo.GetCustomAttribute<SerializeReference>() != null)
                    {
                        fullSuccess = false;
                        errorMessage = $"Field '{fieldName}' on '{componentType.Name}' uses [SerializeReference] and cannot be set via this tool (requires concrete type metadata)";
                        continue;
                    }
                    try
                    {
                        object value = ConvertJTokenToValue(fieldValue, fieldInfo.FieldType);
                        fieldInfo.SetValue(component, value);
                    }
                    catch (Exception ex)
                    {
                        fullSuccess = false;
                        errorMessage = $"Failed to set field '{fieldName}' on '{componentType.Name}': {ex.Message}";
                    }
                    continue;
                }
                
                // Try to update property if not found as a field
                PropertyInfo propertyInfo = componentType.GetProperty(fieldName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (propertyInfo != null)
                {
                    if (!propertyInfo.CanWrite)
                    {
                        fullSuccess = false;
                        errorMessage = $"Property '{fieldName}' on '{componentType.Name}' is read-only";
                        continue;
                    }
                    if (propertyInfo.GetIndexParameters().Length > 0)
                    {
                        fullSuccess = false;
                        errorMessage = $"Property '{fieldName}' on '{componentType.Name}' is an indexer and cannot be set";
                        continue;
                    }
                    try
                    {
                        object value = ConvertJTokenToValue(fieldValue, propertyInfo.PropertyType);
                        propertyInfo.SetValue(component, value);
                    }
                    catch (Exception ex)
                    {
                        fullSuccess = false;
                        errorMessage = $"Failed to set property '{fieldName}' on '{componentType.Name}': {ex.Message}";
                    }
                    continue;
                }
                
                fullSuccess = false;
                errorMessage = $"Field or Property  with name '{fieldName}' not found on component '{componentType.Name}'";
            }

            return fullSuccess;
        }

        /// <summary>
        /// Convert a JToken to a value of the specified type
        /// </summary>
        /// <param name="token">The JToken to convert</param>
        /// <param name="targetType">The target type to convert to</param>
        /// <returns>The converted value</returns>
        private object ConvertJTokenToValue(JToken token, Type targetType)
        {
            if (token == null)
            {
                return null;
            }
            
            // Handle Unity Vector types
            if (targetType == typeof(Vector2) && token.Type == JTokenType.Object)
            {
                JObject vector = (JObject)token;
                return new Vector2(
                    vector["x"]?.ToObject<float>() ?? 0f,
                    vector["y"]?.ToObject<float>() ?? 0f
                );
            }
            
            if (targetType == typeof(Vector3) && token.Type == JTokenType.Object)
            {
                JObject vector = (JObject)token;
                return new Vector3(
                    vector["x"]?.ToObject<float>() ?? 0f,
                    vector["y"]?.ToObject<float>() ?? 0f,
                    vector["z"]?.ToObject<float>() ?? 0f
                );
            }
            
            if (targetType == typeof(Vector4) && token.Type == JTokenType.Object)
            {
                JObject vector = (JObject)token;
                return new Vector4(
                    vector["x"]?.ToObject<float>() ?? 0f,
                    vector["y"]?.ToObject<float>() ?? 0f,
                    vector["z"]?.ToObject<float>() ?? 0f,
                    vector["w"]?.ToObject<float>() ?? 0f
                );
            }
            
            if (targetType == typeof(Quaternion) && token.Type == JTokenType.Object)
            {
                JObject quaternion = (JObject)token;
                return new Quaternion(
                    quaternion["x"]?.ToObject<float>() ?? 0f,
                    quaternion["y"]?.ToObject<float>() ?? 0f,
                    quaternion["z"]?.ToObject<float>() ?? 0f,
                    quaternion["w"]?.ToObject<float>() ?? 1f
                );
            }
            
            if (targetType == typeof(Color) && token.Type == JTokenType.Object)
            {
                JObject color = (JObject)token;
                return new Color(
                    color["r"]?.ToObject<float>() ?? 0f,
                    color["g"]?.ToObject<float>() ?? 0f,
                    color["b"]?.ToObject<float>() ?? 0f,
                    color["a"]?.ToObject<float>() ?? 1f
                );
            }
            
            if (targetType == typeof(Bounds) && token.Type == JTokenType.Object)
            {
                JObject bounds = (JObject)token;
                Vector3 center = bounds["center"]?.ToObject<Vector3>() ?? Vector3.zero;
                Vector3 size = bounds["size"]?.ToObject<Vector3>() ?? Vector3.one;
                return new Bounds(center, size);
            }
            
            if (targetType == typeof(Rect) && token.Type == JTokenType.Object)
            {
                JObject rect = (JObject)token;
                return new Rect(
                    rect["x"]?.ToObject<float>() ?? 0f,
                    rect["y"]?.ToObject<float>() ?? 0f,
                    rect["width"]?.ToObject<float>() ?? 0f,
                    rect["height"]?.ToObject<float>() ?? 0f
                );
            }
            
            // Handle UnityEngine.Object types (Component, GameObject, ScriptableObject, etc.)
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                var (success, value, error) = ResolveUnityObject(token, targetType);
                if (!success)
                    throw new InvalidOperationException(error);
                return value;
            }
            
            // Handle arrays/lists of UnityEngine.Object references
            if (token.Type == JTokenType.Array && targetType.IsArray)
            {
                Type elementType = targetType.GetElementType();
                if (elementType != null && typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                {
                    JArray arr = (JArray)token;
                    var result = Array.CreateInstance(elementType, arr.Count);
                    for (int i = 0; i < arr.Count; i++)
                    {
                        var (ok, val, err) = ResolveUnityObject(arr[i], elementType);
                        if (!ok)
                            throw new InvalidOperationException($"Array element [{i}]: {err}");
                        result.SetValue(val, i);
                    }
                    return result;
                }
            }

            // Handle List<T> of UnityEngine.Object references
            if (token.Type == JTokenType.Array && targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = targetType.GetGenericArguments()[0];
                if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                {
                    JArray arr = (JArray)token;
                    var list = (System.Collections.IList)Activator.CreateInstance(targetType);
                    for (int i = 0; i < arr.Count; i++)
                    {
                        var (ok, val, err) = ResolveUnityObject(arr[i], elementType);
                        if (!ok)
                            throw new InvalidOperationException($"List element [{i}]: {err}");
                        list.Add(val);
                    }
                    return list;
                }
            }
            
            // Handle enum types
            if (targetType.IsEnum)
            {
                // If JToken is a string, try to parse as enum name
                if (token.Type == JTokenType.String)
                {
                    string enumName = token.ToObject<string>();
                    if (Enum.TryParse(targetType, enumName, true, out object result))
                    {
                        return result;
                    }
                    
                    // If parsing fails, try to convert numeric value
                    if (int.TryParse(enumName, out int enumValue))
                    {
                        return Enum.ToObject(targetType, enumValue);
                    }
                }
                // If JToken is a number, convert directly to enum
                else if (token.Type == JTokenType.Integer)
                {
                    return Enum.ToObject(targetType, token.ToObject<int>());
                }
            }
            
            // For other types, use JToken's ToObject method
            try
            {
                return token.ToObject(targetType);
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"[MCP Unity] Error converting value to type {targetType.Name}: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Resolve a JToken to a Unity Object reference.
        /// Supports: instanceId (int or string), scene path, asset path, Resources path,
        /// and explicit { "instanceId": N } or { "assetPath": "..." } objects.
        /// Returns (success, value, error) — on failure, success=false with a message.
        /// </summary>
        private (bool success, UnityEngine.Object value, string error) ResolveUnityObject(JToken token, Type targetType)
        {
            if (token == null || token.Type == JTokenType.Null)
                return (true, null, null);

            // ── Explicit object forms ──────────────────────────────────
            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;

                // { "instanceId": 12345 }
                if (obj["instanceId"] != null)
                {
                    if (!int.TryParse(obj["instanceId"].ToString(), out int id))
                        return (false, null, $"Invalid instanceId value: {obj["instanceId"]}");
                    var resolved = McpObjectId.ToObject(id);
                    if (resolved == null)
                        return (false, null, $"No object found for instanceId {id}");
                    if (resolved is GameObject goInst)
                        return ResolveFromGameObject(goInst, targetType);
                    if (targetType.IsInstanceOfType(resolved))
                        return (true, resolved, null);
                    return (false, null, $"Object '{resolved.name}' (instanceId={id}) is type {resolved.GetType().Name}, expected {targetType.Name}");
                }

                // { "assetPath": "Assets/..." }
                if (obj["assetPath"] != null)
                {
                    string path = obj["assetPath"].ToObject<string>();
                    if (!IsValidAssetPath(path))
                        return (false, null, $"Invalid asset path '{path}': must start with Assets/ or Packages/ and not contain '..'");
                    
                    // Check for sub-asset (e.g. material inside an FBX)
                    string subAssetName = obj["subAssetName"]?.ToObject<string>();
                    if (!string.IsNullOrEmpty(subAssetName))
                    {
                        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (var asset in allAssets)
                        {
                            if (asset != null && asset.name == subAssetName && targetType.IsInstanceOfType(asset))
                                return (true, asset, null);
                        }
                        return (false, null, $"No {targetType.Name} sub-asset named '{subAssetName}' found at '{path}'");
                    }
                    
                    var mainAsset = AssetDatabase.LoadAssetAtPath(path, targetType);
                    if (mainAsset == null)
                        return (false, null, $"No {targetType.Name} found at asset path '{path}'");
                    return (true, mainAsset, null);
                }

                // { "resourcePath": "folder/name" }  (loaded from Resources)
                if (obj["resourcePath"] != null)
                {
                    string resPath = obj["resourcePath"].ToObject<string>();
                    var loaded = UnityEngine.Resources.Load(resPath, targetType);
                    if (loaded == null)
                        return (false, null, $"No {targetType.Name} found at Resources path '{resPath}'");
                    return (true, loaded, null);
                }

                return (false, null, $"Unrecognized object shape for Unity Object reference: {obj}");
            }

            // ── Integer → treat as instanceId ──────────────────────────
            if (token.Type == JTokenType.Integer)
            {
                int id = token.ToObject<int>();
                var obj = McpObjectId.ToObject(id);
                if (obj == null)
                    return (false, null, $"No object found for instanceId {id}");
                if (obj is GameObject goId)
                    return ResolveFromGameObject(goId, targetType);
                if (targetType.IsInstanceOfType(obj))
                    return (true, obj, null);
                return (false, null, $"Object '{obj.name}' (instanceId={id}) is type {obj.GetType().Name}, expected {targetType.Name}");
            }

            // ── String → try multiple resolution strategies ────────────
            if (token.Type == JTokenType.String)
            {
                string value = token.ToObject<string>()?.Trim();

                if (string.IsNullOrEmpty(value))
                    return (true, null, null);

                // 1) Numeric string → instanceId
                if (int.TryParse(value, out int instanceId))
                {
                    var obj = McpObjectId.ToObject(instanceId);
                    if (obj != null)
                    {
                        if (obj is GameObject goNum)
                            return ResolveFromGameObject(goNum, targetType);
                        if (targetType.IsInstanceOfType(obj))
                            return (true, obj, null);
                        return (false, null, $"Object '{obj.name}' (instanceId={instanceId}) is type {obj.GetType().Name}, expected {targetType.Name}");
                    }
                    // Fall through — might be a name that looks like a number
                }

                // 2) Scene GameObject by hierarchy path (e.g. "Player/WeaponMount")
                GameObject go = FindGameObjectInScene(value);
                if (go != null)
                {
                    return ResolveFromGameObject(go, targetType);
                }

                // 3) Asset database path (e.g. "Assets/Materials/MyMat.mat")
                if (value.StartsWith("Assets/") || value.StartsWith("Packages/"))
                {
                    if (!IsValidAssetPath(value))
                        return (false, null, $"Invalid asset path '{value}': must not contain '..'");
                    var asset = AssetDatabase.LoadAssetAtPath(value, targetType);
                    if (asset != null)
                        return (true, asset, null);
                    return (false, null, $"No {targetType.Name} found at asset path '{value}'");
                }

                // 4) Resources path (e.g. "Prefabs/Enemy" loads from Resources/Prefabs/Enemy)
                var resLoad = UnityEngine.Resources.Load(value, targetType);
                if (resLoad != null)
                    return (true, resLoad, null);

                // 5) Try finding asset by name — check for ambiguity
                string[] guids = AssetDatabase.FindAssets($"t:{targetType.Name} {value}");
                var candidates = new List<(string path, UnityEngine.Object obj)>();
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var assetByName = AssetDatabase.LoadAssetAtPath(assetPath, targetType);
                    if (assetByName != null && assetByName.name == value)
                        candidates.Add((assetPath, assetByName));
                }

                if (candidates.Count == 1)
                    return (true, candidates[0].obj, null);
                if (candidates.Count > 1)
                {
                    string paths = string.Join(", ", candidates.Select(c => c.path));
                    return (false, null, $"Ambiguous: {candidates.Count} '{targetType.Name}' assets named '{value}': {paths}");
                }

                return (false, null, $"Could not resolve Unity Object reference '{value}' for type {targetType.Name}");
            }

            // Fallback
            try
            {
                var fallback = token.ToObject(targetType) as UnityEngine.Object;
                return (true, fallback, null);
            }
            catch (Exception ex)
            {
                return (false, null, $"Error converting value to type {targetType.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolve a target type from a GameObject — strict: only GetComponent on the exact object.
        /// </summary>
        private (bool success, UnityEngine.Object value, string error) ResolveFromGameObject(GameObject go, Type targetType)
        {
            if (go == null)
                return (false, null, "GameObject is null");

            if (targetType == typeof(GameObject))
                return (true, go, null);

            Component comp = go.GetComponent(targetType);
            if (comp != null)
                return (true, comp, null);

            return (false, null, $"GameObject '{go.name}' does not have component {targetType.Name}");
        }

        /// <summary>
        /// Validate an asset path: must start with Assets/ or Packages/, no ".." segments.
        /// </summary>
        private bool IsValidAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.Contains("..")) return false;
            if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/")) return false;
            return true;
        }

        /// <summary>
        /// Find a GameObject in any loaded scene by hierarchy path.
        /// </summary>
        private GameObject FindGameObjectInScene(string path)
        {
            // Direct find (works for root objects in active scene)
            GameObject go = GameObject.Find(path);
            if (go != null) return go;

            // Hierarchical find across ALL loaded scenes
            string[] parts = path.Split('/');
            if (parts.Length == 0) return null;

            foreach (var scene in UnityEngine.SceneManagement.SceneManager.GetAllScenes())
            {
                if (!scene.IsValid()) continue;
                GameObject[] roots = scene.GetRootGameObjects();
                foreach (GameObject root in roots)
                {
                    if (root.name == parts[0])
                    {
                        GameObject current = root;
                        for (int i = 1; i < parts.Length; i++)
                        {
                            Transform child = current.transform.Find(parts[i]);
                            if (child == null) goto nextRoot;
                            current = child.gameObject;
                        }
                        return current;
                    }
                    nextRoot:;
                }
            }

            return null;
        }

        /// <summary>
        /// Ensures all component changes are properly marked dirty and saved to disk.
        /// Critical fix for Prefab assets: previous version only modified in-memory state.
        /// Now explicitly calls SavePrefabAsset() so changes appear in .prefab YAML.
        /// </summary>
        private void EnsureChangesSaved(GameObject gameObject, Component component)
        {
            if (component == null || gameObject == null) return;

            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(gameObject);

            if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);

                // Handle actual Prefab assets (not just scene instances)
                // This ensures object reference wiring ("Prefab-Verdrahtung") is serialized to YAML on disk
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
                if (root == null) root = gameObject;

                if (PrefabUtility.IsPartOfPrefabAsset(root))
                {
                    string assetPath = AssetDatabase.GetAssetPath(root);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        PrefabUtility.SavePrefabAsset(root);
                        McpLogger.LogInfo($"[MCP Unity] Saved Prefab asset after update_component: {assetPath}");
                    }
                }
            }
        }
    }
}
