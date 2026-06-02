#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Shared;

namespace UnityMCP.Editor.Tools
{
    internal static class GameObjectToolHelpers
    {
        public static string GetString(JsonElement arguments, string name, string defaultValue = null)
        {
            JsonElement value;
            if (arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return defaultValue;
        }

        public static bool GetBool(JsonElement arguments, string name, bool defaultValue = false)
        {
            JsonElement value;
            if (arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out value))
            {
                if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                {
                    return value.GetBoolean();
                }
            }

            return defaultValue;
        }

        public static int GetInt(JsonElement arguments, string name, int defaultValue = 0)
        {
            JsonElement value;
            if (arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out value) && value.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            return defaultValue;
        }

        public static Vector3 GetVector3(JsonElement arguments, string name, Vector3 defaultValue)
        {
            JsonElement value;
            if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out value))
            {
                return defaultValue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var values = value.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                if (values.Length >= 3)
                {
                    return new Vector3(values[0], values[1], values[2]);
                }
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                float x = defaultValue.x;
                float y = defaultValue.y;
                float z = defaultValue.z;

                JsonElement component;
                if (value.TryGetProperty("x", out component)) x = component.GetSingle();
                if (value.TryGetProperty("y", out component)) y = component.GetSingle();
                if (value.TryGetProperty("z", out component)) z = component.GetSingle();
                return new Vector3(x, y, z);
            }

            return defaultValue;
        }

        public static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            var directType = Type.GetType(typeName, false, true);
            if (directType != null)
            {
                return directType;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var found = assembly.GetTypes().FirstOrDefault(t =>
                        string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        return found;
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var found = ex.Types.Where(t => t != null).FirstOrDefault(t =>
                        string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        public static string GetPath(GameObject go)
        {
            if (go == null)
            {
                return string.Empty;
            }

            var parts = new Stack<string>();
            var current = go.transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        public static GameObject FindByPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            for (var sceneIndex = 0; sceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCount; sceneIndex++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    var found = FindByPathRecursive(root.transform, path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries), 0);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        static GameObject FindByPathRecursive(Transform current, string[] segments, int index)
        {
            if (current == null || segments == null || segments.Length == 0)
            {
                return null;
            }

            if (!string.Equals(current.name, segments[index], StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (index == segments.Length - 1)
            {
                return current.gameObject;
            }

            for (var i = 0; i < current.childCount; i++)
            {
                var found = FindByPathRecursive(current.GetChild(i), segments, index + 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static object BuildHierarchy(GameObject go, int depth, int maxDepth)
        {
            var children = new List<object>();
            if (depth < maxDepth)
            {
                for (var i = 0; i < go.transform.childCount; i++)
                {
                    children.Add(BuildHierarchy(go.transform.GetChild(i).gameObject, depth + 1, maxDepth));
                }
            }

            return new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                active = go.activeSelf,
                layer = go.layer,
                tag = go.tag,
                path = GetPath(go),
                children = children
            };
        }

        public static object BuildHierarchyNode(GameObject go, int depth, int maxDepth)
        {
            var children = new List<object>();
            if (depth < maxDepth)
            {
                for (var i = 0; i < go.transform.childCount; i++)
                {
                    children.Add(BuildHierarchyNode(go.transform.GetChild(i).gameObject, depth + 1, maxDepth));
                }
            }

            return new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                active = go.activeSelf,
                layer = go.layer,
                tag = go.tag,
                path = GetPath(go),
                children = children
            };
        }

        public static GameObject FindGameObject(string name, string tag, string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                return FindByPath(path);
            }

            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
            {
                if (go == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(name) &&
                    go.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(tag) && !go.CompareTag(tag))
                {
                    continue;
                }

                return go;
            }

            return null;
        }

        public static object DescribeComponent(Component component)
        {
            if (component == null)
            {
                return new { type = "MissingScript", instanceId = 0 };
            }

            return new
            {
                type = component.GetType().Name,
                fullTypeName = component.GetType().FullName,
                instanceId = component.GetInstanceID(),
                enabled = component is Behaviour behaviour ? behaviour.enabled : (bool?)null,
                transformPath = component.transform != null ? GetPath(component.gameObject) : string.Empty
            };
        }
    }

    [McpTool("unity.gameobject.create", "Create a new GameObject with optional parent, position, rotation, and scale")]
    public sealed class CreateGameObjectTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", "New GameObject");
            var parentPath = GameObjectToolHelpers.GetString(context.Arguments, "parentPath", GameObjectToolHelpers.GetString(context.Arguments, "parent", null));
            var position = GameObjectToolHelpers.GetVector3(context.Arguments, "position", Vector3.zero);
            var rotation = GameObjectToolHelpers.GetVector3(context.Arguments, "rotation", Vector3.zero);
            var scale = GameObjectToolHelpers.GetVector3(context.Arguments, "scale", Vector3.one);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = new GameObject(name);

                    Transform parent = null;
                    if (!string.IsNullOrWhiteSpace(parentPath))
                    {
                        var parentGo = GameObjectToolHelpers.FindByPath(parentPath) ?? GameObject.Find(parentPath);
                        if (parentGo == null)
                        {
                            UnityEngine.Object.DestroyImmediate(go);
                            return new { success = false, error = $"Parent not found: {parentPath}" };
                        }

                        parent = parentGo.transform;
                    }

                    if (parent != null)
                    {
                        go.transform.SetParent(parent, false);
                    }

                    go.transform.localPosition = position;
                    go.transform.localRotation = Quaternion.Euler(rotation);
                    go.transform.localScale = scale;

                    return new
                    {
                        success = true,
                        gameObject = new
                        {
                            name = go.name,
                            instanceId = go.GetInstanceID(),
                            path = GameObjectToolHelpers.GetPath(go),
                            position = go.transform.position,
                            rotation = go.transform.rotation.eulerAngles,
                            scale = go.transform.localScale
                        }
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.delete", "Delete a GameObject")]
    public sealed class DeleteGameObjectTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var confirm = GameObjectToolHelpers.GetBool(context.Arguments, "confirm", false);

            if (!confirm)
            {
                return new { success = false, error = "Delete confirmation required. Set confirm=true to proceed." };
            }

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = string.IsNullOrWhiteSpace(path) ? $"GameObject not found: {name}" : $"GameObject not found at path: {path}" };
                    }

                    var deletedName = go.name;
                    UnityEngine.Object.DestroyImmediate(go);
                    return new { success = true, deleted = deletedName };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.rename", "Rename a GameObject")]
    public sealed class RenameGameObjectTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var newName = GameObjectToolHelpers.GetString(context.Arguments, "newName", null);

            if (string.IsNullOrWhiteSpace(newName))
            {
                return new { success = false, error = "Missing required argument: newName" };
            }

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = string.IsNullOrWhiteSpace(path) ? $"GameObject not found: {name}" : $"GameObject not found at path: {path}" };
                    }

                    var oldName = go.name;
                    go.name = newName;
                    return new { success = true, previousName = oldName, newName = go.name, path = GameObjectToolHelpers.GetPath(go) };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.find", "Find a GameObject by name, tag, or path")]
    public sealed class FindGameObjectTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var tag = GameObjectToolHelpers.GetString(context.Arguments, "tag", null);
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var includeInactive = GameObjectToolHelpers.GetBool(context.Arguments, "includeInactive", true);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    GameObject go = null;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        go = GameObjectToolHelpers.FindByPath(path);
                    }
                    else
                    {
                        var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None);

                        go = all.FirstOrDefault(candidate =>
                            candidate != null &&
                            (string.IsNullOrWhiteSpace(name) || candidate.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) &&
                            (string.IsNullOrWhiteSpace(tag) || candidate.CompareTag(tag)));
                    }

                    if (go == null)
                    {
                        return new { success = false, error = "GameObject not found" };
                    }

                    return new
                    {
                        success = true,
                        gameObject = new
                        {
                            name = go.name,
                            instanceId = go.GetInstanceID(),
                            path = GameObjectToolHelpers.GetPath(go),
                            active = go.activeSelf,
                            tag = go.tag,
                            layer = go.layer,
                            childCount = go.transform.childCount
                        }
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.get_components", "List all components on a GameObject")]
    public sealed class GetGameObjectComponentsTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = "GameObject not found" };
                    }

                    var components = go.GetComponents<Component>()
                        .Select(GameObjectToolHelpers.DescribeComponent)
                        .ToList();

                    return new
                    {
                        success = true,
                        gameObject = go.name,
                        path = GameObjectToolHelpers.GetPath(go),
                        components = components,
                        count = components.Count
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.add_component", "Add a component to a GameObject by type name")]
    public sealed class AddComponentTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var typeName = GameObjectToolHelpers.GetString(context.Arguments, "componentType", GameObjectToolHelpers.GetString(context.Arguments, "type", null));

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return new { success = false, error = "Missing required argument: type" };
            }

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = "GameObject not found" };
                    }

                    var type = GameObjectToolHelpers.ResolveType(typeName);
                    if (type == null)
                    {
                        return new { success = false, error = $"Component type not found: {typeName}" };
                    }

                    if (!typeof(Component).IsAssignableFrom(type))
                    {
                        return new { success = false, error = $"Type is not a Component: {typeName}" };
                    }

                    var component = go.AddComponent(type);
                    return new
                    {
                        success = true,
                        gameObject = go.name,
                        path = GameObjectToolHelpers.GetPath(go),
                        component = GameObjectToolHelpers.DescribeComponent(component as Component)
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.remove_component", "Remove a component from a GameObject")]
    public sealed class RemoveComponentTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var typeName = GameObjectToolHelpers.GetString(context.Arguments, "componentType", GameObjectToolHelpers.GetString(context.Arguments, "type", null));
            var index = GameObjectToolHelpers.GetInt(context.Arguments, "index", -1);

            if (string.IsNullOrWhiteSpace(typeName) && index < 0)
            {
                return new { success = false, error = "Provide either type or index" };
            }

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = "GameObject not found" };
                    }

                    var components = go.GetComponents<Component>();
                    Component target = null;

                    if (!string.IsNullOrWhiteSpace(typeName))
                    {
                        target = components.FirstOrDefault(c => c != null &&
                            (string.Equals(c.GetType().Name, typeName, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(c.GetType().FullName, typeName, StringComparison.OrdinalIgnoreCase)));
                    }
                    else if (index >= 0 && index < components.Length)
                    {
                        target = components[index];
                    }

                    if (target == null)
                    {
                        return new { success = false, error = "Component not found" };
                    }

                    var removedType = target.GetType().Name;
                    UnityEngine.Object.DestroyImmediate(target);
                    return new { success = true, gameObject = go.name, removedComponent = removedType, path = GameObjectToolHelpers.GetPath(go) };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.set_transform", "Set the transform values of a GameObject")]
    public sealed class SetTransformTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var position = GameObjectToolHelpers.GetVector3(context.Arguments, "position", Vector3.negativeInfinity);
            var rotation = GameObjectToolHelpers.GetVector3(context.Arguments, "rotation", Vector3.negativeInfinity);
            var scale = GameObjectToolHelpers.GetVector3(context.Arguments, "scale", Vector3.negativeInfinity);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = "GameObject not found" };
                    }

                    if (position.x != float.NegativeInfinity)
                    {
                        go.transform.position = position;
                    }

                    if (rotation.x != float.NegativeInfinity)
                    {
                        go.transform.rotation = Quaternion.Euler(rotation);
                    }

                    if (scale.x != float.NegativeInfinity)
                    {
                        go.transform.localScale = scale;
                    }

                    return new
                    {
                        success = true,
                        gameObject = go.name,
                        path = GameObjectToolHelpers.GetPath(go),
                        position = go.transform.position,
                        rotation = go.transform.rotation.eulerAngles,
                        scale = go.transform.localScale
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.set_active", "Set a GameObject active or inactive")]
    public sealed class SetActiveTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var active = GameObjectToolHelpers.GetBool(context.Arguments, "active", true);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = "GameObject not found" };
                    }

                    go.SetActive(active);
                    return new { success = true, gameObject = go.name, active = go.activeSelf, path = GameObjectToolHelpers.GetPath(go) };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.set_parent", "Set the parent of a GameObject")]
    public sealed class SetParentTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var parentPath = GameObjectToolHelpers.GetString(context.Arguments, "parentPath", GameObjectToolHelpers.GetString(context.Arguments, "parent", null));
            var worldPositionStays = GameObjectToolHelpers.GetBool(context.Arguments, "worldPositionStays", true);

            if (string.IsNullOrWhiteSpace(parentPath))
            {
                return new { success = false, error = "Missing required argument: parent" };
            }

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = "GameObject not found" };
                    }

                    var parent = GameObjectToolHelpers.FindByPath(parentPath) ?? GameObject.Find(parentPath);
                    if (parent == null)
                    {
                        return new { success = false, error = $"Parent not found: {parentPath}" };
                    }

                    go.transform.SetParent(parent.transform, worldPositionStays);
                    return new
                    {
                        success = true,
                        gameObject = go.name,
                        parent = parent.name,
                        path = GameObjectToolHelpers.GetPath(go)
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.duplicate", "Duplicate a GameObject")]
    public sealed class DuplicateGameObjectTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var newName = GameObjectToolHelpers.GetString(context.Arguments, "newName", null);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = "GameObject not found" };
                    }

                    var clone = UnityEngine.Object.Instantiate(go, go.transform.parent);
                    clone.name = string.IsNullOrWhiteSpace(newName) ? go.name + " (Clone)" : newName;
                    clone.SetActive(go.activeSelf);

                    return new
                    {
                        success = true,
                        source = go.name,
                        duplicate = new
                        {
                            name = clone.name,
                            instanceId = clone.GetInstanceID(),
                            path = GameObjectToolHelpers.GetPath(clone)
                        }
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.gameobject.get_hierarchy", "Get children of a GameObject")]
    public sealed class GetGameObjectHierarchyTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = GameObjectToolHelpers.GetString(context.Arguments, "path", null);
            var name = GameObjectToolHelpers.GetString(context.Arguments, "name", null);
            var maxDepth = GameObjectToolHelpers.GetInt(context.Arguments, "maxDepth", 10);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var go = GameObjectToolHelpers.FindGameObject(name, null, path);
                    if (go == null)
                    {
                        return new { success = false, error = "GameObject not found" };
                    }

                    var children = new List<object>();
                    for (var i = 0; i < go.transform.childCount; i++)
                    {
                        children.Add(GameObjectToolHelpers.BuildHierarchyNode(go.transform.GetChild(i).gameObject, 1, maxDepth));
                    }

                    return new
                    {
                        success = true,
                        gameObject = go.name,
                        path = GameObjectToolHelpers.GetPath(go),
                        children = children,
                        childCount = children.Count
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }
}
#endif
