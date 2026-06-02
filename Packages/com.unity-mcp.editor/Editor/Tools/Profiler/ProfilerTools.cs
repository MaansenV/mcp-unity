#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityMCP.Editor;
using UnityMCP.Shared;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityMCP.Editor.Tools
{
    internal sealed class ProfilerSample
    {
        public DateTime TimestampUtc { get; set; }
        public long TotalAllocatedMemory { get; set; }
        public long TotalReservedMemory { get; set; }
        public long TotalUnusedReservedMemory { get; set; }
        public long MonoUsedSize { get; set; }
        public long MonoHeapSize { get; set; }
        public int DrawCalls { get; set; }
        public int Triangles { get; set; }
        public int Vertices { get; set; }
        public int Batches { get; set; }
        public int SetPassCalls { get; set; }
        public double MainThreadMs { get; set; }
        public double ScriptUpdateMs { get; set; }
        public long GCAllocBytes { get; set; }
    }

    internal static class ProfilerSampleStore
    {
        static readonly List<ProfilerSample> s_Samples = new List<ProfilerSample>();
        static double s_LastSampleTime;

        static readonly ProfilerRecorder s_GcAllocRecorder = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
        static readonly ProfilerRecorder s_MainThreadRecorder = StartRecorder(ProfilerCategory.Internal, "Main Thread");
        static readonly ProfilerRecorder s_ScriptUpdateRecorder = StartRecorder(ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate");

        static ProfilerSampleStore()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        public static IReadOnlyList<ProfilerSample> Snapshot()
        {
            return s_Samples.ToArray();
        }

        public static void Add(ProfilerSample sample)
        {
            if (sample == null)
            {
                return;
            }

            s_Samples.Add(sample);
        }

        public static void Clear()
        {
            s_Samples.Clear();
            s_LastSampleTime = 0d;
        }

        static void OnEditorUpdate()
        {
            if (!Profiler.enabled)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now - s_LastSampleTime < 0.5d)
            {
                return;
            }

            s_LastSampleTime = now;
            s_Samples.Add(CaptureSample());

            if (s_Samples.Count > 500)
            {
                s_Samples.RemoveRange(0, s_Samples.Count - 500);
            }
        }

        static ProfilerSample CaptureSample()
        {
            return new ProfilerSample
            {
                TimestampUtc = DateTime.UtcNow,
                TotalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong(),
                TotalReservedMemory = Profiler.GetTotalReservedMemoryLong(),
                TotalUnusedReservedMemory = Profiler.GetTotalUnusedReservedMemoryLong(),
                MonoUsedSize = Profiler.GetMonoUsedSizeLong(),
                MonoHeapSize = Profiler.GetMonoHeapSizeLong(),
                DrawCalls = UnityStats.drawCalls,
                Triangles = UnityStats.triangles,
                Vertices = UnityStats.vertices,
                Batches = UnityStats.batches,
                SetPassCalls = UnityStats.setPassCalls,
                MainThreadMs = RecorderToMilliseconds(s_MainThreadRecorder),
                ScriptUpdateMs = RecorderToMilliseconds(s_ScriptUpdateRecorder),
                GCAllocBytes = RecorderToLong(s_GcAllocRecorder)
            };
        }

        static ProfilerRecorder StartRecorder(ProfilerCategory category, string markerName)
        {
            try
            {
                return ProfilerRecorder.StartNew(category, markerName);
            }
            catch
            {
                return default;
            }
        }

        static long RecorderToLong(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue : 0L;
        }

        static double RecorderToMilliseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
            {
                return 0d;
            }

            return recorder.LastValue / 1_000_000d;
        }

        public static Task<object?> ToggleModuleAsync(ToolContext context, bool enabled)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var moduleName = JsonArgReader.ReadString(context.Arguments, "moduleName", string.Empty);
                if (string.IsNullOrWhiteSpace(moduleName))
                {
                    return (object?)new { success = false, error = "Missing moduleName" };
                }

                if (!Enum.TryParse(moduleName, true, out ProfilerArea area))
                {
                    return (object?)new { success = false, error = $"Unknown profiler module: {moduleName}" };
                }

                try
                {
                    ProfilerDriver.SetAreaEnabled(area, enabled);
                    return (object?)new
                    {
                        success = true,
                        moduleName,
                        enabled
                    };
                }
                catch (Exception ex)
                {
                    return (object?)new
                    {
                        success = false,
                        moduleName,
                        enabled,
                        error = ex.Message
                    };
                }
            });
        }

        public static string BuildSnapshotPayload()
        {
            var samples = Snapshot().Select(sample => new
            {
                timestampUtc = sample.TimestampUtc,
                totalAllocatedMemory = sample.TotalAllocatedMemory,
                totalReservedMemory = sample.TotalReservedMemory,
                totalUnusedReservedMemory = sample.TotalUnusedReservedMemory,
                monoUsedSize = sample.MonoUsedSize,
                monoHeapSize = sample.MonoHeapSize,
                drawCalls = sample.DrawCalls,
                triangles = sample.Triangles,
                vertices = sample.Vertices,
                batches = sample.Batches,
                setPassCalls = sample.SetPassCalls,
                mainThreadMs = sample.MainThreadMs,
                scriptUpdateMs = sample.ScriptUpdateMs,
                gcAllocBytes = sample.GCAllocBytes
            }).ToArray();

            var payload = new
            {
                success = true,
                capturedAtUtc = DateTime.UtcNow,
                profilingEnabled = Profiler.enabled,
                samples
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpTool("unity.profiler.start", "Start profiling")]
    public sealed class ProfilerStartTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                Profiler.enabled = true;
                return (object?)new
                {
                    success = true,
                    isProfiling = Profiler.enabled
                };
            });
        }
    }

    [McpTool("unity.profiler.stop", "Stop profiling")]
    public sealed class ProfilerStopTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                Profiler.enabled = false;
                return (object?)new
                {
                    success = true,
                    isProfiling = Profiler.enabled
                };
            });
        }
    }

    [McpTool("unity.profiler.get_status", "Get profiler status")]
    public sealed class ProfilerGetStatusTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                return (object?)new
                {
                    success = true,
                    isProfiling = Profiler.enabled,
                    sampleCount = ProfilerSampleStore.Snapshot().Count
                };
            });
        }
    }

    [McpTool("unity.profiler.get_memory_stats", "Get current memory statistics")]
    public sealed class ProfilerGetMemoryStatsTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                return (object?)new
                {
                    success = true,
                    totalMemory = Profiler.GetTotalAllocatedMemoryLong(),
                    totalReserved = Profiler.GetTotalReservedMemoryLong(),
                    totalUnusedReserved = Profiler.GetTotalUnusedReservedMemoryLong(),
                    monoUsed = Profiler.GetMonoUsedSizeLong(),
                    monoHeap = Profiler.GetMonoHeapSizeLong()
                };
            });
        }
    }

    [McpTool("unity.profiler.get_rendering_stats", "Get current rendering statistics")]
    public sealed class ProfilerGetRenderingStatsTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                return (object?)new
                {
                    success = true,
                    drawCalls = UnityStats.drawCalls,
                    triangles = UnityStats.triangles,
                    vertices = UnityStats.vertices,
                    batches = UnityStats.batches,
                    setPassCalls = UnityStats.setPassCalls,
                    shadowCasters = UnityStats.shadowCasters
                };
            });
        }
    }

    [McpTool("unity.profiler.get_script_stats", "Get current script execution statistics")]
    public sealed class ProfilerGetScriptStatsTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var samples = ProfilerSampleStore.Snapshot();
                var latest = samples.Count > 0 ? samples[samples.Count - 1] : null;

                return (object?)new
                {
                    success = true,
                    sampleCount = samples.Count,
                    latest = latest == null ? null : new
                    {
                        timestampUtc = latest.TimestampUtc,
                        mainThreadMs = latest.MainThreadMs,
                        scriptUpdateMs = latest.ScriptUpdateMs,
                        gcAllocBytes = latest.GCAllocBytes
                    }
                };
            });
        }
    }

    [McpTool("unity.profiler.get_samples", "Get recent profiler samples")]
    public sealed class ProfilerGetSamplesTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var limit = JsonArgReader.ReadInt(context.Arguments, "limit", 50);
                var samples = ProfilerSampleStore.Snapshot().ToList();

                if (limit > 0 && samples.Count > limit)
                {
                    samples = samples.Skip(samples.Count - limit).ToList();
                }

                return (object?)new
                {
                    success = true,
                    sampleCount = samples.Count,
                    samples = samples.Select(sample => new
                    {
                        timestampUtc = sample.TimestampUtc,
                        totalAllocatedMemory = sample.TotalAllocatedMemory,
                        totalReservedMemory = sample.TotalReservedMemory,
                        totalUnusedReservedMemory = sample.TotalUnusedReservedMemory,
                        monoUsedSize = sample.MonoUsedSize,
                        monoHeapSize = sample.MonoHeapSize,
                        drawCalls = sample.DrawCalls,
                        triangles = sample.Triangles,
                        vertices = sample.Vertices,
                        batches = sample.Batches,
                        setPassCalls = sample.SetPassCalls,
                        mainThreadMs = sample.MainThreadMs,
                        scriptUpdateMs = sample.ScriptUpdateMs,
                        gcAllocBytes = sample.GCAllocBytes
                    }).ToArray()
                };
            });
        }
    }

    [McpTool("unity.profiler.enable_module", "Enable a profiler module")]
    public sealed class ProfilerEnableModuleTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return ProfilerSampleStore.ToggleModuleAsync(context, true);
        }
    }

    [McpTool("unity.profiler.disable_module", "Disable a profiler module")]
    public sealed class ProfilerDisableModuleTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return ProfilerSampleStore.ToggleModuleAsync(context, false);
        }
    }

    [McpTool("unity.profiler.save_data", "Save profiler data to a file")]
    public sealed class ProfilerSaveDataTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var path = JsonArgReader.ReadString(context.Arguments, "path", string.Empty);
                if (string.IsNullOrWhiteSpace(path))
                {
                    return (object?)new { success = false, error = "Missing path" };
                }

                var payload = ProfilerSampleStore.BuildSnapshotPayload();
                File.WriteAllText(path, payload);

                return (object?)new
                {
                    success = true,
                    path,
                    bytesWritten = new FileInfo(path).Length
                };
            });
        }
    }

    [McpTool("unity.profiler.load_data", "Load profiler data from a file")]
    public sealed class ProfilerLoadDataTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var path = JsonArgReader.ReadString(context.Arguments, "path", string.Empty);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return (object?)new { success = false, error = "File not found", path };
                }

                var json = File.ReadAllText(path);
                try
                {
                    var doc = JsonDocument.Parse(json);
                    var loadedSamples = new List<ProfilerSample>();

                    if (doc.RootElement.TryGetProperty("samples", out var samplesElement) && samplesElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var sample in samplesElement.EnumerateArray())
                        {
                            loadedSamples.Add(ParseSample(sample));
                        }
                    }

                    ProfilerSampleStore.Clear();
                    foreach (var sample in loadedSamples)
                    {
                        ProfilerSampleStore.Add(sample);
                    }

                    return (object?)new
                    {
                        success = true,
                        path,
                        loadedSamples = loadedSamples.Count
                    };
                }
                catch (Exception ex)
                {
                    return (object?)new { success = false, error = ex.Message, path };
                }
            });
        }

        static ProfilerSample ParseSample(JsonElement sample)
        {
            return new ProfilerSample
            {
                TimestampUtc = sample.TryGetProperty("timestampUtc", out var ts) && ts.ValueKind == JsonValueKind.String && DateTime.TryParse(ts.GetString(), out var timestamp)
                    ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                TotalAllocatedMemory = ReadLong(sample, "totalAllocatedMemory"),
                TotalReservedMemory = ReadLong(sample, "totalReservedMemory"),
                TotalUnusedReservedMemory = ReadLong(sample, "totalUnusedReservedMemory"),
                MonoUsedSize = ReadLong(sample, "monoUsedSize"),
                MonoHeapSize = ReadLong(sample, "monoHeapSize"),
                DrawCalls = ReadInt(sample, "drawCalls"),
                Triangles = ReadInt(sample, "triangles"),
                Vertices = ReadInt(sample, "vertices"),
                Batches = ReadInt(sample, "batches"),
                SetPassCalls = ReadInt(sample, "setPassCalls"),
                MainThreadMs = ReadDouble(sample, "mainThreadMs"),
                ScriptUpdateMs = ReadDouble(sample, "scriptUpdateMs"),
                GCAllocBytes = ReadLong(sample, "gcAllocBytes")
            };
        }

        static long ReadLong(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : 0L;
        }

        static int ReadInt(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;
        }

        static double ReadDouble(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.TryGetDouble(out var result) ? result : 0d;
        }

    }

    [McpTool("unity.profiler.clear_data", "Clear stored profiler samples")]
    public sealed class ProfilerClearDataTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                ProfilerSampleStore.Clear();
                return (object?)new
                {
                    success = true
                };
            });
        }
    }
}
#endif
