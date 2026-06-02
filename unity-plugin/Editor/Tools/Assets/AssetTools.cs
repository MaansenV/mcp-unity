#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Shared;

namespace UnityMCP.Editor.Tools
{
    internal static class ToolContextExtensions
    {
        public static string GetString(this ToolContext ctx, string name, string defaultValue = "")
        {
            if (ctx.Arguments.ValueKind == JsonValueKind.Object && ctx.Arguments.TryGetProperty(name, out var value))
            {
                return value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined
                    ? defaultValue
                    : value.GetString() ?? defaultValue;
            }

            return defaultValue;
        }

        public static int GetInt(this ToolContext ctx, string name, int defaultValue = 0)
        {
            if (ctx.Arguments.ValueKind == JsonValueKind.Object && ctx.Arguments.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
                {
                    return intValue;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out intValue))
                {
                    return intValue;
                }
            }

            return defaultValue;
        }

        public static float GetFloat(this ToolContext ctx, string name, float defaultValue = 0f)
        {
            if (ctx.Arguments.ValueKind == JsonValueKind.Object && ctx.Arguments.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var floatValue))
                {
                    return floatValue;
                }

                if (value.ValueKind == JsonValueKind.String && float.TryParse(value.GetString(), out floatValue))
                {
                    return floatValue;
                }
            }

            return defaultValue;
        }

        public static bool GetBool(this ToolContext ctx, string name, bool defaultValue = false)
        {
            if (ctx.Arguments.ValueKind == JsonValueKind.Object && ctx.Arguments.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (value.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var boolValue))
                {
                    return boolValue;
                }
            }

            return defaultValue;
        }
    }

    internal static class AssetToolHelpers
    {
        static readonly MethodInfo s_MoveAssetToTrash = typeof(AssetDatabase).GetMethod("MoveAssetToTrash", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);

        public static string ResolveAssetPath(string pathOrGuid)
        {
            return AssetPathValidator.ResolveAssetPath(pathOrGuid);
        }

        public static string ResolveProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        /// <summary>
        /// Convert an asset path to an absolute path.
        /// WARNING: Only use for read operations. For write operations, use AssetPathValidator.TryValidateAssetPath first.
        /// </summary>
        public static string ProjectRelativeToAbsolute(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
            {
                return assetPath;
            }

            return Path.GetFullPath(Path.Combine(ResolveProjectRoot(), assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static string AbsoluteToAssetPath(string absolutePath)
        {
            var projectRoot = Path.GetFullPath(ResolveProjectRoot()).Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
            var normalized = Path.GetFullPath(absolutePath).Replace('/', Path.DirectorySeparatorChar);

            if (!normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var relative = normalized.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        public static void EnsureAssetFolderExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || folderPath == "Assets")
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var normalized = folderPath.Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                normalized = $"Assets/{normalized}".TrimEnd('/');
            }

            var parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        public static object ReadSerializedPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.longValue;
                case SerializedPropertyType.Boolean:
                    return property.boolValue;
                case SerializedPropertyType.Float:
                    return property.doubleValue;
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Color:
                    return new { r = property.colorValue.r, g = property.colorValue.g, b = property.colorValue.b, a = property.colorValue.a };
                case SerializedPropertyType.ObjectReference:
                    return new
                    {
                        name = property.objectReferenceValue ? property.objectReferenceValue.name : null,
                        type = property.objectReferenceValue ? property.objectReferenceValue.GetType().Name : null,
                        path = property.objectReferenceValue ? AssetDatabase.GetAssetPath(property.objectReferenceValue) : null,
                        guid = property.objectReferenceValue ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(property.objectReferenceValue)) : null
                    };
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex;
                case SerializedPropertyType.Vector2:
                    return new { x = property.vector2Value.x, y = property.vector2Value.y };
                case SerializedPropertyType.Vector3:
                    return new { x = property.vector3Value.x, y = property.vector3Value.y, z = property.vector3Value.z };
                case SerializedPropertyType.Vector4:
                    return new { x = property.vector4Value.x, y = property.vector4Value.y, z = property.vector4Value.z, w = property.vector4Value.w };
                case SerializedPropertyType.Rect:
                    return new { x = property.rectValue.x, y = property.rectValue.y, width = property.rectValue.width, height = property.rectValue.height };
                case SerializedPropertyType.Bounds:
                    return new
                    {
                        center = new { x = property.boundsValue.center.x, y = property.boundsValue.center.y, z = property.boundsValue.center.z },
                        size = new { x = property.boundsValue.size.x, y = property.boundsValue.size.y, z = property.boundsValue.size.z }
                    };
                default:
                    return property.propertyType.ToString();
            }
        }

        public static object GetImporterSettings(AssetImporter importer)
        {
            if (importer == null)
            {
                return new { available = false };
            }

            var serialized = new SerializedObject(importer);
            var iterator = serialized.GetIterator();
            var enterChildren = true;
            var settings = new List<object>();

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.name == "m_Script")
                {
                    continue;
                }

                settings.Add(new
                {
                    name = iterator.name,
                    displayName = iterator.displayName,
                    propertyType = iterator.propertyType.ToString(),
                    value = ReadSerializedPropertyValue(iterator)
                });
            }

            return new
            {
                available = true,
                importerType = importer.GetType().Name,
                assetBundleName = importer.assetBundleName,
                assetBundleVariant = importer.assetBundleVariant,
                userData = importer.userData,
                settings
            };
        }

        public static object CreateError(string message, Exception ex = null)
        {
            return new
            {
                success = false,
                error = new
                {
                    message,
                    exceptionType = ex?.GetType().Name,
                    details = ex?.Message,
                    stackTrace = ex?.StackTrace
                }
            };
        }

        public static bool MoveAssetToTrash(string path)
        {
            if (s_MoveAssetToTrash == null)
            {
                return AssetDatabase.DeleteAsset(path);
            }

            var result = s_MoveAssetToTrash.Invoke(null, new object[] { path });
            return result is bool boolResult ? boolResult : false;
        }
    }

    [McpTool("unity.asset.find", "Find assets by name, type, or tag with pagination")]
    public sealed class FindAssetsTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var query = ctx.GetString("query", string.Empty);
                    var type = ctx.GetString("type", string.Empty);
                    var tag = ctx.GetString("tag", string.Empty);
                    var limit = Math.Max(1, ctx.GetInt("limit", 100));
                    var cursor = Math.Max(0, ctx.GetInt("cursor", 0));
                    var searchInFolders = ctx.GetString("folder", string.Empty);

                    var terms = new List<string>();
                    if (!string.IsNullOrWhiteSpace(query)) terms.Add(query.Trim());
                    if (!string.IsNullOrWhiteSpace(type)) terms.Add($"t:{type.Trim()}");
                    if (!string.IsNullOrWhiteSpace(tag)) terms.Add($"l:{tag.Trim()}");

                    var filter = string.Join(" ", terms);
                    var folders = string.IsNullOrWhiteSpace(searchInFolders) ? Array.Empty<string>() : new[] { AssetToolHelpers.ResolveAssetPath(searchInFolders) };
                    var guids = folders.Length > 0 ? AssetDatabase.FindAssets(filter, folders) : AssetDatabase.FindAssets(filter);

                    var results = guids
                        .Skip(cursor)
                        .Take(limit)
                        .Select(guid =>
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guid);
                            var asset = AssetDatabase.LoadMainAssetAtPath(path);
                            return new
                            {
                                guid,
                                path,
                                name = asset ? asset.name : Path.GetFileNameWithoutExtension(path),
                                type = asset ? asset.GetType().Name : "Unknown",
                                isFolder = AssetDatabase.IsValidFolder(path),
                                labels = asset ? AssetDatabase.GetLabels(asset) : Array.Empty<string>()
                            };
                        })
                        .ToList();

                    return new
                    {
                        success = true,
                        query,
                        type,
                        tag,
                        cursor,
                        limit,
                        total = guids.Length,
                        hasMore = cursor + limit < guids.Length,
                        assets = results
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to find assets", ex);
            }
        }
    }

    [McpTool("unity.asset.get_info", "Get asset metadata and importer settings")]
    public sealed class GetAssetInfoTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var identifier = ctx.GetString("path", ctx.GetString("guid", string.Empty));
                    var path = AssetToolHelpers.ResolveAssetPath(identifier);

                    if (string.IsNullOrWhiteSpace(path) || (!AssetDatabase.IsValidFolder(path) && AssetDatabase.LoadMainAssetAtPath(path) == null))
                    {
                        return AssetToolHelpers.CreateError($"Asset not found: {identifier}");
                    }

                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    var importer = AssetImporter.GetAtPath(path);
                    var fileInfo = File.Exists(AssetToolHelpers.ProjectRelativeToAbsolute(path)) ? new FileInfo(AssetToolHelpers.ProjectRelativeToAbsolute(path)) : null;

                    return new
                    {
                        success = true,
                        asset = new
                        {
                            path,
                            guid,
                            name = asset ? asset.name : Path.GetFileNameWithoutExtension(path),
                            type = asset ? asset.GetType().FullName : (AssetDatabase.IsValidFolder(path) ? "Folder" : "Unknown"),
                            isFolder = AssetDatabase.IsValidFolder(path),
                            labels = asset ? AssetDatabase.GetLabels(asset) : Array.Empty<string>(),
                            mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(path)?.FullName,
                            dependencyCount = AssetDatabase.GetDependencies(path, true).Length,
                            fileSize = fileInfo?.Length,
                            createdUtc = fileInfo?.CreationTimeUtc,
                            modifiedUtc = fileInfo?.LastWriteTimeUtc
                        },
                        importer = AssetToolHelpers.GetImporterSettings(importer),
                        subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                            .Select(x => new { name = x.name, type = x.GetType().FullName })
                            .ToArray()
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to get asset info", ex);
            }
        }
    }

    [McpTool("unity.asset.import", "Import an external file into the project")]
    public sealed class ImportAssetTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var sourcePath = ctx.GetString("sourcePath", ctx.GetString("path", string.Empty));
                    var destinationPath = ctx.GetString("destinationPath", string.Empty);

                    if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                    {
                        return AssetToolHelpers.CreateError($"Source file not found: {sourcePath}");
                    }

                    var fileName = Path.GetFileName(sourcePath);
                    if (string.IsNullOrWhiteSpace(destinationPath))
                    {
                        destinationPath = $"Assets/Imported/{fileName}";
                    }

                    destinationPath = destinationPath.Replace('\\', '/');
                    if (!destinationPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        destinationPath = $"Assets/{destinationPath.TrimStart('/') }".Replace("//", "/");
                    }

                    var folder = Path.GetDirectoryName(destinationPath)?.Replace('\\', '/').Replace("\\", "/") ?? "Assets";
                    AssetToolHelpers.EnsureAssetFolderExists(folder);

                    destinationPath = AssetDatabase.GenerateUniqueAssetPath(destinationPath);
                    var absoluteDestination = AssetToolHelpers.ProjectRelativeToAbsolute(destinationPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(absoluteDestination) ?? AssetToolHelpers.ResolveProjectRoot());
                    File.Copy(sourcePath, absoluteDestination, true);
                    AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceUpdate);

                    return new
                    {
                        success = true,
                        sourcePath,
                        assetPath = destinationPath,
                        guid = AssetDatabase.AssetPathToGUID(destinationPath),
                        name = Path.GetFileNameWithoutExtension(destinationPath)
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to import asset", ex);
            }
        }
    }

    [McpTool("unity.asset.delete", "Delete an asset with optional trash support")]
    public sealed class DeleteAssetTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var identifier = ctx.GetString("path", ctx.GetString("guid", string.Empty));
                    var trash = ctx.GetBool("trash", true);
                    var path = AssetToolHelpers.ResolveAssetPath(identifier);

                    if (string.IsNullOrWhiteSpace(path) || (!AssetDatabase.IsValidFolder(path) && AssetDatabase.LoadMainAssetAtPath(path) == null))
                    {
                        return AssetToolHelpers.CreateError($"Asset not found: {identifier}");
                    }

                    var deleted = trash ? AssetToolHelpers.MoveAssetToTrash(path) : AssetDatabase.DeleteAsset(path);
                    return new
                    {
                        success = deleted,
                        path,
                        guid = AssetDatabase.AssetPathToGUID(path),
                        trashed = trash && deleted
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to delete asset", ex);
            }
        }
    }

    [McpTool("unity.asset.move", "Move an asset to a new path")]
    public sealed class MoveAssetTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var sourceIdentifier = ctx.GetString("sourcePath", ctx.GetString("path", ctx.GetString("guid", string.Empty)));
                    var destinationPath = ctx.GetString("destinationPath", string.Empty).Replace('\\', '/');
                    var sourcePath = AssetToolHelpers.ResolveAssetPath(sourceIdentifier);

                    if (string.IsNullOrWhiteSpace(sourcePath) || (!AssetDatabase.IsValidFolder(sourcePath) && AssetDatabase.LoadMainAssetAtPath(sourcePath) == null))
                    {
                        return AssetToolHelpers.CreateError($"Asset not found: {sourceIdentifier}");
                    }

                    if (string.IsNullOrWhiteSpace(destinationPath))
                    {
                        return AssetToolHelpers.CreateError("destinationPath is required");
                    }

                    if (!destinationPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        destinationPath = $"Assets/{destinationPath.TrimStart('/') }".Replace("//", "/");
                    }

                    var folder = Path.GetDirectoryName(destinationPath)?.Replace('\\', '/') ?? "Assets";
                    AssetToolHelpers.EnsureAssetFolderExists(folder);
                    destinationPath = AssetDatabase.GenerateUniqueAssetPath(destinationPath);

                    var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        return AssetToolHelpers.CreateError(error);
                    }

                    return new
                    {
                        success = true,
                        sourcePath,
                        destinationPath,
                        guid = AssetDatabase.AssetPathToGUID(destinationPath)
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to move asset", ex);
            }
        }
    }

    [McpTool("unity.asset.copy", "Copy an asset to a new path")]
    public sealed class CopyAssetTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var sourceIdentifier = ctx.GetString("sourcePath", ctx.GetString("path", ctx.GetString("guid", string.Empty)));
                    var destinationPath = ctx.GetString("destinationPath", string.Empty).Replace('\\', '/');
                    var sourcePath = AssetToolHelpers.ResolveAssetPath(sourceIdentifier);

                    if (string.IsNullOrWhiteSpace(sourcePath) || (!AssetDatabase.IsValidFolder(sourcePath) && AssetDatabase.LoadMainAssetAtPath(sourcePath) == null))
                    {
                        return AssetToolHelpers.CreateError($"Asset not found: {sourceIdentifier}");
                    }

                    if (string.IsNullOrWhiteSpace(destinationPath))
                    {
                        return AssetToolHelpers.CreateError("destinationPath is required");
                    }

                    if (!destinationPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        destinationPath = $"Assets/{destinationPath.TrimStart('/') }".Replace("//", "/");
                    }

                    var folder = Path.GetDirectoryName(destinationPath)?.Replace('\\', '/') ?? "Assets";
                    AssetToolHelpers.EnsureAssetFolderExists(folder);
                    destinationPath = AssetDatabase.GenerateUniqueAssetPath(destinationPath);

                    var success = AssetDatabase.CopyAsset(sourcePath, destinationPath);
                    if (!success)
                    {
                        return AssetToolHelpers.CreateError("CopyAsset failed");
                    }

                    return new
                    {
                        success = true,
                        sourcePath,
                        destinationPath,
                        guid = AssetDatabase.AssetPathToGUID(destinationPath)
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to copy asset", ex);
            }
        }
    }

    [McpTool("unity.asset.create_folder", "Create an asset folder")]
    public sealed class CreateFolderTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var folderName = ctx.GetString("folderName", ctx.GetString("name", string.Empty));
                    var parentPath = ctx.GetString("parentPath", "Assets").Replace('\\', '/');

                    // Support "path" as a combined parameter (e.g. "Assets/Foo/Bar")
                    var combinedPath = ctx.GetString("path", string.Empty);
                    if (!string.IsNullOrWhiteSpace(combinedPath) && string.IsNullOrWhiteSpace(folderName))
                    {
                        combinedPath = combinedPath.Replace('\\', '/');
                        if (!combinedPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                        {
                            combinedPath = "Assets/" + combinedPath.TrimStart('/');
                        }
                        var lastSlash = combinedPath.LastIndexOf('/');
                        if (lastSlash > 0)
                        {
                            parentPath = combinedPath.Substring(0, lastSlash);
                            folderName = combinedPath.Substring(lastSlash + 1);
                        }
                        else
                        {
                            parentPath = "Assets";
                            folderName = combinedPath;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(folderName))
                    {
                        return AssetToolHelpers.CreateError("folderName is required");
                    }

                    if (!parentPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        parentPath = $"Assets/{parentPath.TrimStart('/') }".Replace("//", "/");
                    }

                    AssetToolHelpers.EnsureAssetFolderExists(parentPath);
                    var guid = AssetDatabase.CreateFolder(parentPath, folderName);
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        return AssetToolHelpers.CreateError("Failed to create folder");
                    }

                    var created = AssetDatabase.GUIDToAssetPath(guid);

                    return new
                    {
                        success = true,
                        path = created,
                        guid
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to create folder", ex);
            }
        }
    }

    [McpTool("unity.asset.refresh", "Refresh the Unity asset database")]
    public sealed class RefreshAssetsTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var force = ctx.GetBool("force", false);
                    AssetDatabase.Refresh();
                    return new { success = true, refreshed = true, force };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to refresh assets", ex);
            }
        }
    }

    [McpTool("unity.asset.get_dependencies", "Get asset dependencies")]
    public sealed class GetAssetDependenciesTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var identifier = ctx.GetString("path", ctx.GetString("guid", string.Empty));
                    var recursive = ctx.GetBool("recursive", true);
                    var path = AssetToolHelpers.ResolveAssetPath(identifier);

                    if (string.IsNullOrWhiteSpace(path) || (!AssetDatabase.IsValidFolder(path) && AssetDatabase.LoadMainAssetAtPath(path) == null))
                    {
                        return AssetToolHelpers.CreateError($"Asset not found: {identifier}");
                    }

                    var dependencies = AssetDatabase.GetDependencies(path, recursive)
                        .Select(dep => new
                        {
                            path = dep,
                            guid = AssetDatabase.AssetPathToGUID(dep),
                            isFolder = AssetDatabase.IsValidFolder(dep)
                        })
                        .ToArray();

                    return new
                    {
                        success = true,
                        path,
                        recursive,
                        total = dependencies.Length,
                        dependencies
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to get dependencies", ex);
            }
        }
    }

    [McpTool("unity.asset.get_guid", "Get the GUID for an asset path")]
    public sealed class GetAssetGuidTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var identifier = ctx.GetString("path", ctx.GetString("guid", string.Empty));
                    var path = AssetToolHelpers.ResolveAssetPath(identifier);
                    var guid = AssetDatabase.AssetPathToGUID(path);

                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        return AssetToolHelpers.CreateError($"Unable to resolve GUID for: {identifier}");
                    }

                    return new
                    {
                        success = true,
                        path,
                        guid
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to get GUID", ex);
            }
        }
    }

    [McpTool("unity.asset.get_path", "Get the asset path for a GUID")]
    public sealed class GetAssetPathTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var guid = ctx.GetString("guid", string.Empty);
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        return AssetToolHelpers.CreateError("guid is required");
                    }

                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return AssetToolHelpers.CreateError($"Unable to resolve path for GUID: {guid}");
                    }

                    return new
                    {
                        success = true,
                        guid,
                        path
                    };
                });
            }
            catch (Exception ex)
            {
                return AssetToolHelpers.CreateError("Failed to get asset path", ex);
            }
        }
    }
}
#endif
