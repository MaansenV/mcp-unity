#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMCP.Editor;
using UnityMCP.Shared;

namespace UnityMCP.Editor.Tools
{
    static class ToolArg
    {
        public static T Get<T>(ToolContext ctx, string name, T defaultValue = default)
        {
            if (ctx.Arguments.ValueKind == JsonValueKind.Object && ctx.Arguments.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
                {
                    return defaultValue;
                }

                try
                {
                    var parsed = JsonSerializer.Deserialize<T>(value.GetRawText());
                    return parsed == null ? defaultValue : parsed;
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }
    }

    static class PrefabToolHelpers
    {
        public static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        }

        public static void EnsureAssetDirectory(string assetPath)
        {
            assetPath = NormalizePath(assetPath);
            var dir = NormalizePath(Path.GetDirectoryName(assetPath) ?? string.Empty);
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(dir))
            {
                return;
            }

            var parts = dir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            var current = parts[0];
            if (!AssetDatabase.IsValidFolder(current))
            {
                if (!string.Equals(current, "Assets", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        public static GameObject ResolveGameObject(int instanceId)
        {
            return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
        }
    }

    [McpTool("unity.prefab.create", "Create a prefab from a GameObject in the scene")]
    internal sealed class PrefabCreateTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var gameObjectId = ToolArg.Get(context, "gameObjectId", 0);
                var path = PrefabToolHelpers.NormalizePath(ToolArg.Get(context, "path", "Assets/Prefabs/NewPrefab.prefab"));

                var go = PrefabToolHelpers.ResolveGameObject(gameObjectId);
                if (go == null)
                {
                    return new { success = false, error = $"GameObject not found with ID: {gameObjectId}" };
                }

                PrefabToolHelpers.EnsureAssetDirectory(path);

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out var success);
                return new
                {
                    success,
                    path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : path,
                    name = prefab != null ? prefab.name : go.name
                };
            });
        }
    }

    [McpTool("unity.prefab.instantiate", "Instantiate a prefab into the scene")]
    internal sealed class PrefabInstantiateTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var path = PrefabToolHelpers.NormalizePath(ToolArg.Get(context, "prefabPath", ToolArg.Get(context, "path", string.Empty)));
                var posX = ToolArg.Get(context, "positionX", 0f);
                var posY = ToolArg.Get(context, "positionY", 0f);
                var posZ = ToolArg.Get(context, "positionZ", 0f);
                var rotX = ToolArg.Get(context, "rotationX", 0f);
                var rotY = ToolArg.Get(context, "rotationY", 0f);
                var rotZ = ToolArg.Get(context, "rotationZ", 0f);
                var parentPath = ToolArg.Get(context, "parentPath", string.Empty);
                var parentId = string.IsNullOrEmpty(parentPath) ? ToolArg.Get(context, "parentGameObjectId", 0) : 0;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    return new { success = false, error = $"Prefab not found at path: {path}" };
                }

                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    return new { success = false, error = $"Failed to instantiate prefab: {path}" };
                }

                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentGo = GameObject.Find(parentPath);
                    if (parentGo != null)
                    {
                        instance.transform.SetParent(parentGo.transform, false);
                    }
                }
                else if (parentId != 0)
                {
                    var parent = PrefabToolHelpers.ResolveGameObject(parentId);
                    if (parent != null)
                    {
                        instance.transform.SetParent(parent.transform, false);
                    }
                }

                instance.transform.position = new Vector3(posX, posY, posZ);
                instance.transform.rotation = Quaternion.Euler(rotX, rotY, rotZ);
                Selection.activeGameObject = instance;

                return new
                {
                    success = true,
                    instanceId = instance.GetInstanceID(),
                    name = instance.name,
                    path
                };
            });
        }
    }

    [McpTool("unity.prefab.save", "Save prefab changes")]
    internal sealed class PrefabSaveTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var gameObjectId = ToolArg.Get(context, "gameObjectId", 0);
                var path = PrefabToolHelpers.NormalizePath(ToolArg.Get(context, "prefabPath", ToolArg.Get(context, "path", string.Empty)));

                var go = PrefabToolHelpers.ResolveGameObject(gameObjectId);
                if (go == null)
                {
                    return new { success = false, error = $"GameObject not found with ID: {gameObjectId}" };
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    path = AssetDatabase.GetAssetPath(go);
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    return new { success = false, error = "Unable to determine prefab path" };
                }

                PrefabToolHelpers.EnsureAssetDirectory(path);
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out var success);

                return new
                {
                    success,
                    path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : path,
                    name = prefab != null ? prefab.name : go.name
                };
            });
        }
    }

    [McpTool("unity.prefab.open", "Open a prefab for editing")]
    internal sealed class PrefabOpenTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var path = PrefabToolHelpers.NormalizePath(ToolArg.Get(context, "prefabPath", ToolArg.Get(context, "path", string.Empty)));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    return new { success = false, error = $"Prefab not found at path: {path}" };
                }

                PrefabStageUtility.OpenPrefab(path);
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                return new
                {
                    success = stage != null,
                    path,
                    stage = stage != null ? stage.prefabAssetPath : null
                };
            });
        }
    }

    [McpTool("unity.prefab.close", "Close prefab editing")]
    internal sealed class PrefabCloseTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage == null)
                {
                    return new { success = true, message = "No prefab stage is currently open" };
                }

                StageUtility.GoToMainStage();
                return new
                {
                    success = true,
                    closed = stage.prefabAssetPath
                };
            });
        }
    }

    [McpTool("unity.prefab.get_info", "Get prefab metadata")]
    internal sealed class PrefabGetInfoTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var gameObjectId = ToolArg.Get(context, "gameObjectId", 0);
                var path = PrefabToolHelpers.NormalizePath(ToolArg.Get(context, "prefabPath", ToolArg.Get(context, "path", string.Empty)));

                GameObject go = null;
                if (gameObjectId != 0)
                {
                    go = PrefabToolHelpers.ResolveGameObject(gameObjectId);
                }

                if (go == null && !string.IsNullOrWhiteSpace(path))
                {
                    go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }

                if (go == null)
                {
                    return new { success = false, error = "Prefab or GameObject not found" };
                }

                var source = PrefabUtility.GetCorrespondingObjectFromSource(go) as GameObject;
                return new
                {
                    success = true,
                    name = go.name,
                    assetPath = AssetDatabase.GetAssetPath(go),
                    prefabAssetType = PrefabUtility.GetPrefabAssetType(go).ToString(),
                    prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(go).ToString(),
                    isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(go),
                    isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(go),
                    isPrefabInstanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go,
                    sourcePrefabName = source != null ? source.name : null,
                    sourcePrefabPath = source != null ? AssetDatabase.GetAssetPath(source) : null
                };
            });
        }
    }

    [McpTool("unity.prefab.apply_overrides", "Apply prefab instance overrides")]
    internal sealed class PrefabApplyOverridesTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var prefabPath = ToolArg.Get(context, "prefabPath", string.Empty);
                var gameObjectId = string.IsNullOrEmpty(prefabPath) ? ToolArg.Get(context, "gameObjectId", 0) : 0;
                GameObject go = null;
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                }
                if (go == null && gameObjectId != 0)
                {
                    go = PrefabToolHelpers.ResolveGameObject(gameObjectId);
                }
                if (go == null)
                {
                    return new { success = false, error = string.IsNullOrEmpty(prefabPath) ? $"GameObject not found with ID: {gameObjectId}" : $"Prefab not found at path: {prefabPath}" };
                }

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? PrefabUtility.GetNearestPrefabInstanceRoot(go);
                if (root == null)
                {
                    return new { success = false, error = "Object is not part of a prefab instance" };
                }

                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
                return new
                {
                    success = true,
                    instanceRoot = root.name
                };
            });
        }
    }

    [McpTool("unity.prefab.revert_overrides", "Revert prefab instance to original")]
    internal sealed class PrefabRevertOverridesTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var prefabPath = ToolArg.Get(context, "prefabPath", string.Empty);
                var gameObjectId = string.IsNullOrEmpty(prefabPath) ? ToolArg.Get(context, "gameObjectId", 0) : 0;
                GameObject go = null;
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                }
                if (go == null && gameObjectId != 0)
                {
                    go = PrefabToolHelpers.ResolveGameObject(gameObjectId);
                }
                if (go == null)
                {
                    return new { success = false, error = string.IsNullOrEmpty(prefabPath) ? $"GameObject not found with ID: {gameObjectId}" : $"Prefab not found at path: {prefabPath}" };
                }

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? PrefabUtility.GetNearestPrefabInstanceRoot(go);
                if (root == null)
                {
                    return new { success = false, error = "Object is not part of a prefab instance" };
                }

                PrefabUtility.RevertPrefabInstance(root, InteractionMode.UserAction);
                return new
                {
                    success = true,
                    instanceRoot = root.name
                };
            });
        }
    }
}
#endif
