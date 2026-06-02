#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMCP.Editor;
using UnityMCP.Shared;

namespace UnityMCP.Editor.Tools
{
    internal static class SceneToolHelpers
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

        public static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var parts = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
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

            var components = go.GetComponents<Component>()
                .Select(component => component != null ? component.GetType().Name : "MissingScript")
                .ToList();

            return new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                active = go.activeSelf,
                path = GetTransformPath(go.transform),
                components = components,
                children = children
            };
        }
    }

    [McpTool("unity.scene.list", "List all scenes in the build settings and currently open scenes")]
    public sealed class ListScenesTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            try
            {
                var scenes = await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var results = new List<object>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var scene in EditorBuildSettings.scenes)
                    {
                        if (string.IsNullOrEmpty(scene.path))
                        {
                            continue;
                        }

                        if (seen.Add(scene.path))
                        {
                            results.Add(new
                            {
                                path = scene.path,
                                name = System.IO.Path.GetFileNameWithoutExtension(scene.path),
                                enabled = scene.enabled,
                                inBuildSettings = true,
                                isOpen = false
                            });
                        }
                    }

                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        var key = string.IsNullOrEmpty(scene.path) ? $"__open_{i}" : scene.path;
                        if (!seen.Add(key))
                        {
                            continue;
                        }

                        results.Add(new
                        {
                            path = scene.path,
                            name = scene.name,
                            isLoaded = scene.isLoaded,
                            isDirty = scene.isDirty,
                            rootCount = scene.rootCount,
                            inBuildSettings = false,
                            isOpen = true
                        });
                    }

                    return results;
                });

                return new { success = true, scenes = scenes };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.scene.get_active", "Get the currently active scene")]
    public sealed class GetActiveSceneTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            try
            {
                var sceneInfo = await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var scene = SceneManager.GetActiveScene();
                    return new
                    {
                        name = scene.name,
                        path = scene.path,
                        isDirty = scene.isDirty,
                        isLoaded = scene.isLoaded,
                        rootCount = scene.rootCount,
                        rootObjects = scene.GetRootGameObjects().Select(go => new
                        {
                            name = go.name,
                            instanceId = go.GetInstanceID(),
                            path = SceneToolHelpers.GetTransformPath(go.transform)
                        }).ToList()
                    };
                });

                return new { success = true, scene = sceneInfo };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.scene.set_active", "Set a loaded scene as the active scene")]
    public sealed class SetActiveSceneTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = SceneToolHelpers.GetString(context.Arguments, "path", null);
            if (string.IsNullOrWhiteSpace(path))
            {
                return new { success = false, error = "Missing required argument: path" };
            }

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    if (!scene.IsValid())
                    {
                        return new { success = false, error = $"Scene not found or not loaded: {path}" };
                    }

                    var success = SceneManager.SetActiveScene(scene);
                    return new { success, scene = scene.name, path = scene.path };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.scene.open", "Open a scene in the editor")]
    public sealed class OpenSceneTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = SceneToolHelpers.GetString(context.Arguments, "path", null);
            var additive = SceneToolHelpers.GetBool(context.Arguments, "additive", false);

            if (string.IsNullOrWhiteSpace(path))
            {
                return new { success = false, error = "Missing required argument: path" };
            }

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var options = additive ? OpenSceneMode.Additive : OpenSceneMode.Single;
                    var scene = EditorSceneManager.OpenScene(path, options);
                    return new
                    {
                        success = true,
                        scene = scene.name,
                        path = scene.path,
                        additive
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.scene.save", "Save the current scene or a specific scene")]
    public sealed class SaveSceneTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var path = SceneToolHelpers.GetString(context.Arguments, "path", null);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var activeScene = SceneManager.GetActiveScene();
                    bool success;

                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        success = EditorSceneManager.SaveScene(activeScene, path);
                    }
                    else
                    {
                        success = EditorSceneManager.SaveScene(activeScene);
                    }

                    return new { success, scene = activeScene.name, path = activeScene.path };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.scene.create", "Create a new empty scene")]
    public sealed class CreateSceneTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var setupGameObjects = SceneToolHelpers.GetBool(context.Arguments, "setupGameObjects", true);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var scene = EditorSceneManager.NewScene(
                        setupGameObjects ? NewSceneSetup.DefaultGameObjects : NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);

                    return new
                    {
                        success = true,
                        scene = scene.name,
                        path = scene.path,
                        setupGameObjects
                    };
                });
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.scene.get_hierarchy", "Get the full hierarchy of the current scene")]
    public sealed class GetSceneHierarchyTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var maxDepth = SceneToolHelpers.GetInt(context.Arguments, "maxDepth", 10);

            try
            {
                var hierarchy = await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var scene = SceneManager.GetActiveScene();
                    var roots = scene.GetRootGameObjects();
                    return roots.Select(go => SceneToolHelpers.BuildHierarchy(go, 0, maxDepth)).ToList();
                });

                return new { success = true, hierarchy = hierarchy };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }

    [McpTool("unity.scene.find_objects", "Find objects in the scene by name or type")]
    public sealed class FindSceneObjectsTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext context)
        {
            var name = SceneToolHelpers.GetString(context.Arguments, "name", null);
            var typeName = SceneToolHelpers.GetString(context.Arguments, "type", null);
            var includeInactive = SceneToolHelpers.GetBool(context.Arguments, "includeInactive", true);

            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var results = new List<object>();

                    if (!string.IsNullOrWhiteSpace(typeName))
                    {
                        var type = SceneToolHelpers.ResolveType(typeName);
                        if (type == null)
                        {
                            return new { success = false, error = $"Type not found: {typeName}" };
                        }

                        var objects = UnityEngine.Object.FindObjectsByType(type,
                            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None);

                        foreach (var item in objects)
                        {
                            if (item == null)
                            {
                                continue;
                            }

                            var objectName = item.name;
                            if (!string.IsNullOrWhiteSpace(name) &&
                                objectName.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                continue;
                            }

                            var gameObject = item as GameObject;
                            var component = item as Component;
                            results.Add(new
                            {
                                name = objectName,
                                instanceId = item.GetInstanceID(),
                                type = item.GetType().Name,
                                path = gameObject != null ? SceneToolHelpers.GetTransformPath(gameObject.transform) : component != null ? SceneToolHelpers.GetTransformPath(component.transform) : string.Empty
                            });
                        }
                    }
                    else
                    {
                        var objects = UnityEngine.Object.FindObjectsByType<GameObject>(
                            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None);

                        foreach (var go in objects)
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

                            results.Add(new
                            {
                                name = go.name,
                                instanceId = go.GetInstanceID(),
                                type = go.GetType().Name,
                                path = SceneToolHelpers.GetTransformPath(go.transform)
                            });
                        }
                    }

                    return new { success = true, objects = results, count = results.Count };
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
