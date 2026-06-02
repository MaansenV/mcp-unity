#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityMCP.Editor;
using UnityMCP.Shared;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tools
{
    [McpTool("unity.editor.get_state", "Get the current editor state")]
    public sealed class EditorGetStateTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                return (object?)new
                {
                    success = true,
                    isPlaying = EditorApplication.isPlaying,
                    isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode,
                    isPaused = EditorApplication.isPaused,
                    isCompiling = EditorApplication.isCompiling,
                    isUpdating = EditorApplication.isUpdating,
                    playbackTime = EditorApplication.timeSinceStartup,
                    applicationPath = EditorApplication.applicationPath,
                    unityVersion = Application.unityVersion,
                    activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString()
                };
            });
        }
    }

    [McpTool("unity.editor.play", "Enter play mode")]
    public sealed class EditorPlayTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                EditorApplication.isPlaying = true;
                return (object?)new
                {
                    success = true,
                    isPlaying = EditorApplication.isPlaying
                };
            });
        }
    }

    [McpTool("unity.editor.stop", "Exit play mode")]
    public sealed class EditorStopTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                EditorApplication.isPlaying = false;
                return (object?)new
                {
                    success = true,
                    isPlaying = EditorApplication.isPlaying
                };
            });
        }
    }

    [McpTool("unity.editor.pause", "Toggle pause")]
    public sealed class EditorPauseTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                EditorApplication.isPaused = !EditorApplication.isPaused;
                return (object?)new
                {
                    success = true,
                    isPaused = EditorApplication.isPaused
                };
            });
        }
    }

    [McpTool("unity.editor.get_selection", "Get the current selection")]
    public sealed class EditorGetSelectionTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var objects = Selection.objects.Select(BuildSelectionItem).ToArray();

                return (object?)new
                {
                    success = true,
                    activeInstanceId = Selection.activeInstanceID,
                    activeObject = BuildSelectionItem(Selection.activeObject),
                    count = objects.Length,
                    selection = objects
                };
            });
        }

        static object? BuildSelectionItem(UnityEngine.Object? unityObject)
        {
            if (unityObject == null)
            {
                return null;
            }

            return new
            {
                instanceId = unityObject.GetInstanceID(),
                name = unityObject.name,
                type = unityObject.GetType().FullName,
                assetPath = AssetDatabase.GetAssetPath(unityObject)
            };
        }
    }

    [McpTool("unity.editor.set_selection", "Select objects by instanceId")]
    public sealed class EditorSetSelectionTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var ids = ReadInstanceIds(context.Arguments);
                var objects = ids
                    .Select(EditorUtility.InstanceIDToObject)
                    .Where(o => o != null)
                    .ToArray();

                Selection.objects = objects!;

                return (object?)new
                {
                    success = true,
                    selectedCount = Selection.objects.Length,
                    activeInstanceId = Selection.activeInstanceID
                };
            });
        }

        static int[] ReadInstanceIds(JsonElement args)
        {
            if (args.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<int>();
            }

            if (args.TryGetProperty("instanceIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
            {
                var list = new List<int>();
                foreach (var id in ids.EnumerateArray())
                {
                    if (id.TryGetInt32(out var value))
                    {
                        list.Add(value);
                    }
                }

                if (list.Count > 0)
                {
                    return list.ToArray();
                }
            }

            if (args.TryGetProperty("instanceId", out var single) && single.TryGetInt32(out var singleId))
            {
                return new[] { singleId };
            }

            return Array.Empty<int>();
        }
    }

    [McpTool("unity.editor.ping_object", "Ping an object in the Project window")]
    public sealed class EditorPingObjectTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var instanceId = JsonArgReader.ReadInt(context.Arguments, "instanceId", 0);
                var unityObject = EditorUtility.InstanceIDToObject(instanceId);

                if (unityObject != null)
                {
                    EditorGUIUtility.PingObject(unityObject);
                }

                return (object?)new
                {
                    success = unityObject != null,
                    instanceId,
                    name = unityObject != null ? unityObject.name : string.Empty
                };
            });
        }
    }

    [McpTool("unity.editor.execute_menu_item", "Execute any menu command")]
    public sealed class EditorExecuteMenuItemTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var menuPath = JsonArgReader.ReadString(context.Arguments, "menuPath", string.Empty);
                var success = !string.IsNullOrWhiteSpace(menuPath) && EditorApplication.ExecuteMenuItem(menuPath);

                return (object?)new
                {
                    success,
                    menuPath
                };
            });
        }
    }

    [McpTool("unity.editor.undo", "Undo the last operation")]
    public sealed class EditorUndoTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                Undo.PerformUndo();
                return (object?)new
                {
                    success = true
                };
            });
        }
    }

    [McpTool("unity.editor.redo", "Redo the last undone operation")]
    public sealed class EditorRedoTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                Undo.PerformRedo();
                return (object?)new
                {
                    success = true
                };
            });
        }
    }
}
#endif
