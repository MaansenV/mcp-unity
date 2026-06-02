#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityMCP.Editor;
using UnityMCP.Shared;
using UnityEngine;

namespace UnityMCP.Editor.Tools
{
    internal sealed class ConsoleLogEntry
    {
        public long Sequence { get; set; }
        public DateTime TimestampUtc { get; set; }
        public LogType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
    }

    internal static class ConsoleLogStore
    {
        private const int MaxEntries = 1000;

        static readonly ConcurrentQueue<ConsoleLogEntry> s_Entries = new ConcurrentQueue<ConsoleLogEntry>();
        static long s_Sequence;
        static int s_ErrorCount;
        static int s_WarningCount;
        static int s_InfoCount;
        static int s_Subscribed;

        static ConsoleLogStore()
        {
            SubscribeToLogs();
        }

        public static void SubscribeToLogs()
        {
            if (Interlocked.Exchange(ref s_Subscribed, 1) == 1)
            {
                return;
            }

            Application.logMessageReceivedThreaded += OnLogMessageReceived;
        }

        public static void Clear()
        {
            while (s_Entries.TryDequeue(out _))
            {
            }

            Interlocked.Exchange(ref s_ErrorCount, 0);
            Interlocked.Exchange(ref s_WarningCount, 0);
            Interlocked.Exchange(ref s_InfoCount, 0);

            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            var clearMethod = logEntriesType?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            clearMethod?.Invoke(null, null);
        }

        public static int TotalCount => s_Entries.Count;
        public static int ErrorCount => Volatile.Read(ref s_ErrorCount);
        public static int WarningCount => Volatile.Read(ref s_WarningCount);
        public static int InfoCount => Volatile.Read(ref s_InfoCount);

        static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            var entry = new ConsoleLogEntry
            {
                Sequence = Interlocked.Increment(ref s_Sequence),
                TimestampUtc = DateTime.UtcNow,
                Type = type,
                Message = condition ?? string.Empty,
                StackTrace = stackTrace ?? string.Empty
            };

            switch (type)
            {
                case LogType.Warning:
                    Interlocked.Increment(ref s_WarningCount);
                    break;
                case LogType.Log:
                    Interlocked.Increment(ref s_InfoCount);
                    break;
                case LogType.Assert:
                case LogType.Error:
                case LogType.Exception:
                    Interlocked.Increment(ref s_ErrorCount);
                    break;
                default:
                    Interlocked.Increment(ref s_InfoCount);
                    break;
            }

            s_Entries.Enqueue(entry);
            while (s_Entries.Count > MaxEntries && s_Entries.TryDequeue(out _))
            {
            }
        }

        public static IReadOnlyList<ConsoleLogEntry> SnapshotEntries()
        {
            return s_Entries.ToArray();
        }
    }

    [McpTool("unity.console.get_logs", "Get console log entries with optional filters")]
    public sealed class GetConsoleLogsTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var maxEntries = JsonArgReader.ReadInt(context.Arguments, "maxEntries", 200);
                var search = JsonArgReader.ReadString(context.Arguments, "search", string.Empty);
                var includeStackTrace = JsonArgReader.ReadBool(context.Arguments, "includeStackTrace", true);
                var clearAfterRead = JsonArgReader.ReadBool(context.Arguments, "clearAfterRead", false);
                var types = JsonArgReader.ReadStringArray(context.Arguments, "types");
                var sinceSequence = JsonArgReader.ReadLong(context.Arguments, "sinceSequence", 0);

                var entries = ConsoleLogStore.SnapshotEntries()
                    .Where(e => e.Sequence > sinceSequence)
                    .Where(e => string.IsNullOrWhiteSpace(search) ||
                                e.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                (includeStackTrace && e.StackTrace.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Where(e => types.Length == 0 || types.Any(t => string.Equals(t, e.Type.ToString(), StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(e => e.Sequence)
                    .ToList();

                if (maxEntries > 0 && entries.Count > maxEntries)
                {
                    entries = entries.Skip(entries.Count - maxEntries).ToList();
                }

                var result = new
                {
                    success = true,
                    totalCount = ConsoleLogStore.TotalCount,
                    errorCount = ConsoleLogStore.ErrorCount,
                    warningCount = ConsoleLogStore.WarningCount,
                    infoCount = ConsoleLogStore.InfoCount,
                    consoleCount = TryGetInternalConsoleCount(),
                    subscribed = true,
                    logs = entries.Select(entry => new
                    {
                        sequence = entry.Sequence,
                        timestampUtc = entry.TimestampUtc,
                        type = entry.Type.ToString(),
                        message = entry.Message,
                        stackTrace = includeStackTrace ? entry.StackTrace : string.Empty
                    }).ToArray()
                };

                if (clearAfterRead)
                {
                    ConsoleLogStore.Clear();
                }

                return (object?)result;
            });
        }

        static int? TryGetInternalConsoleCount()
        {
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            var getCountMethod = logEntriesType?.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (getCountMethod == null)
            {
                return null;
            }

            try
            {
                return Convert.ToInt32(getCountMethod.Invoke(null, null));
            }
            catch
            {
                return null;
            }
        }
    }

    [McpTool("unity.console.clear", "Clear the Unity console")]
    public sealed class ClearConsoleTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                ConsoleLogStore.Clear();
                return (object?)new
                {
                    success = true
                };
            });
        }
    }

    [McpTool("unity.console.get_count", "Get console counts by severity")]
    public sealed class GetConsoleCountTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                return (object?)new
                {
                    success = true,
                    totalCount = ConsoleLogStore.TotalCount,
                    errors = ConsoleLogStore.ErrorCount,
                    warnings = ConsoleLogStore.WarningCount,
                    info = ConsoleLogStore.InfoCount,
                    consoleCount = TryGetInternalConsoleCount()
                };
            });
        }

        static int? TryGetInternalConsoleCount()
        {
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            var getCountMethod = logEntriesType?.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (getCountMethod == null)
            {
                return null;
            }

            try
            {
                return Convert.ToInt32(getCountMethod.Invoke(null, null));
            }
            catch
            {
                return null;
            }
        }
    }

    [McpTool("unity.console.subscribe", "Start receiving new console logs")]
    public sealed class SubscribeConsoleTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                ConsoleLogStore.SubscribeToLogs();
                return (object?)new
                {
                    success = true,
                    subscribed = true,
                    totalCount = ConsoleLogStore.TotalCount,
                    errorCount = ConsoleLogStore.ErrorCount,
                    warningCount = ConsoleLogStore.WarningCount,
                    infoCount = ConsoleLogStore.InfoCount
                };
            });
        }
    }

    internal static class JsonArgReader
    {
        public static string ReadString(JsonElement args, string name, string defaultValue)
        {
            return args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? defaultValue
                : defaultValue;
        }

        public static int ReadInt(JsonElement args, string name, int defaultValue)
        {
            if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.TryGetInt32(out var result))
            {
                return result;
            }

            return defaultValue;
        }

        public static long ReadLong(JsonElement args, string name, long defaultValue)
        {
            if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.TryGetInt64(out var result))
            {
                return result;
            }

            return defaultValue;
        }

        public static bool ReadBool(JsonElement args, string name, bool defaultValue)
        {
            if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
            {
                return value.GetBoolean();
            }

            return defaultValue;
        }

        public static string[] ReadStringArray(JsonElement args, string name)
        {
            if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
            {
                return Array.Empty<string>();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                var items = new List<string>();
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            items.Add(s);
                        }
                    }
                }

                return items.ToArray();
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var s = value.GetString();
                return string.IsNullOrWhiteSpace(s) ? Array.Empty<string>() : new[] { s };
            }

            return Array.Empty<string>();
        }
    }
}
#endif
