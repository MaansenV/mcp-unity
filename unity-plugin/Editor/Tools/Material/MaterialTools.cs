#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Shared;

namespace UnityMCP.Editor.Tools
{
    internal static class MaterialToolHelpers
    {
        public static Material ResolveMaterial(string pathOrGuid)
        {
            var path = AssetToolHelpers.ResolveAssetPath(pathOrGuid);
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        public static Shader ResolveShader(string shaderName)
        {
            if (string.IsNullOrWhiteSpace(shaderName))
            {
                return Shader.Find("Standard");
            }

            return Shader.Find(shaderName) ?? Shader.Find("Standard");
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

        public static object DescribeProperty(Material material, int index)
        {
            var shader = material.shader;
            var type = ShaderUtil.GetPropertyType(shader, index);
            var name = ShaderUtil.GetPropertyName(shader, index);

            object value = null;
            switch (type)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    value = material.HasProperty(name) ? material.GetColor(name) : (object)null;
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    value = material.HasProperty(name) ? (object)material.GetVector(name) : null;
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    value = material.HasProperty(name) ? material.GetFloat(name) : (object)null;
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    value = material.HasProperty(name) ? material.GetTexture(name) : null;
                    break;
            }

            return new
            {
                name,
                displayName = ShaderUtil.GetPropertyDescription(shader, index),
                type = type.ToString(),
                hidden = ShaderUtil.IsShaderPropertyHidden(shader, index),
                attributes = Array.Empty<string>(),
                flags = string.Empty,
                value = value is UnityEngine.Object obj && obj != null
                    ? new { name = obj.name, type = obj.GetType().Name, path = AssetDatabase.GetAssetPath(obj), guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)) }
                    : value
            };
        }

        public static Material FindMaterial(string identifier)
        {
            var path = AssetToolHelpers.ResolveAssetPath(identifier);
            if (!string.IsNullOrWhiteSpace(path))
            {
                var byPath = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (byPath != null)
                {
                    return byPath;
                }
            }

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return null;
            }

            return Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == identifier);
        }

        public static object SetProperty<T>(ToolContext ctx, Action<Material, string, T> setter, T value)
        {
            try
            {
                var material = FindMaterial(ctx.GetString("materialPath", ctx.GetString("path", ctx.GetString("guid", string.Empty))));
                var property = ctx.GetString("propertyName", ctx.GetString("property", ctx.GetString("name", string.Empty)));

                if (material == null) return CreateError("Material not found");
                if (string.IsNullOrWhiteSpace(property)) return CreateError("property is required");
                if (!material.HasProperty(property)) return CreateError($"Material does not have property: {property}");

                setter(material, property, value);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return new
                {
                    success = true,
                    path = AssetDatabase.GetAssetPath(material),
                    guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material)),
                    property,
                    value
                };
            }
            catch (Exception ex)
            {
                return CreateError("Failed to set material property", ex);
            }
        }

        public static object GetProperty<T>(ToolContext ctx, Func<Material, string, T> getter)
        {
            try
            {
                var material = FindMaterial(ctx.GetString("materialPath", ctx.GetString("path", ctx.GetString("guid", string.Empty))));
                var property = ctx.GetString("propertyName", ctx.GetString("property", ctx.GetString("name", string.Empty)));

                if (material == null) return CreateError("Material not found");
                if (string.IsNullOrWhiteSpace(property)) return CreateError("property is required");
                if (!material.HasProperty(property)) return CreateError($"Material does not have property: {property}");

                var value = getter(material, property);
                return new
                {
                    success = true,
                    path = AssetDatabase.GetAssetPath(material),
                    guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material)),
                    property,
                    value
                };
            }
            catch (Exception ex)
            {
                return CreateError("Failed to get material property", ex);
            }
        }
    }

    [McpTool("unity.material.create", "Create a new material with an optional shader")]
    public sealed class CreateMaterialTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var name = ctx.GetString("name", "New Material");
                    var shaderName = ctx.GetString("shaderName", ctx.GetString("shader", string.Empty));
                    var path = ctx.GetString("path", string.Empty).Replace('\\', '/');
                    var shader = MaterialToolHelpers.ResolveShader(shaderName);
                    var material = new Material(shader) { name = name };

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        path = AssetDatabase.GenerateUniqueAssetPath($"Assets/{name}.mat");
                    }
                    else if (!path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        path = $"Assets/{path.TrimStart('/') }".Replace("//", "/");
                    }

                    AssetToolHelpers.EnsureAssetFolderExists(Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets");
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                    AssetDatabase.CreateAsset(material, path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    return new
                    {
                        success = true,
                        path,
                        guid = AssetDatabase.AssetPathToGUID(path),
                        name = material.name,
                        shader = material.shader ? material.shader.name : null
                    };
                });
            }
            catch (Exception ex)
            {
                return MaterialToolHelpers.CreateError("Failed to create material", ex);
            }
        }
    }

    [McpTool("unity.material.set_color", "Set a material color property")]
    public sealed class SetMaterialColorTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            return await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var colorHex = ctx.GetString("color", string.Empty);
                var color = new Color(
                    ctx.GetFloat("r", 0f),
                    ctx.GetFloat("g", 0f),
                    ctx.GetFloat("b", 0f),
                    ctx.GetFloat("a", 1f));

                if (!string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out var parsedColor))
                {
                    color = parsedColor;
                }
                return MaterialToolHelpers.SetProperty(ctx, (m, p, v) => m.SetColor(p, (Color)v), color);
            });
        }
    }

    [McpTool("unity.material.set_float", "Set a material float property")]
    public sealed class SetMaterialFloatTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            return await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var value = ctx.GetFloat("value", ctx.GetFloat("float", 0f));
                return MaterialToolHelpers.SetProperty(ctx, (m, p, v) => m.SetFloat(p, Convert.ToSingle(v)), value);
            });
        }
    }

    [McpTool("unity.material.set_texture", "Set a material texture property")]
    public sealed class SetMaterialTextureTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            return await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var material = MaterialToolHelpers.FindMaterial(ctx.GetString("materialPath", ctx.GetString("path", ctx.GetString("guid", string.Empty))));
                var property = ctx.GetString("propertyName", ctx.GetString("property", ctx.GetString("name", string.Empty)));
                var texturePath = ctx.GetString("texturePath", ctx.GetString("value", string.Empty));
                var texture = string.IsNullOrWhiteSpace(texturePath) ? null : AssetDatabase.LoadAssetAtPath<Texture>(AssetToolHelpers.ResolveAssetPath(texturePath));

                if (material == null) return MaterialToolHelpers.CreateError("Material not found");
                if (string.IsNullOrWhiteSpace(property)) return MaterialToolHelpers.CreateError("property is required");

                if (!material.HasProperty(property)) return MaterialToolHelpers.CreateError($"Material does not have property: {property}");

                material.SetTexture(property, texture);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return new { success = true, path = AssetDatabase.GetAssetPath(material), guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material)), property, texture = texturePath };
            });
        }
    }

    [McpTool("unity.material.set_vector", "Set a material vector property")]
    public sealed class SetMaterialVectorTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            return await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var value = new Vector4(ctx.GetFloat("x", 0), ctx.GetFloat("y", 0), ctx.GetFloat("z", 0), ctx.GetFloat("w", 0));
                return MaterialToolHelpers.SetProperty(ctx, (m, p, v) => m.SetVector(p, (Vector4)v), value);
            });
        }
    }

    [McpTool("unity.material.set_int", "Set a material int property")]
    public sealed class SetMaterialIntTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            return await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var value = ctx.GetInt("value", ctx.GetInt("int", 0));
                return MaterialToolHelpers.SetProperty(ctx, (m, p, v) => m.SetInt(p, Convert.ToInt32(v)), value);
            });
        }
    }

    [McpTool("unity.material.get_properties", "List all material shader properties")]
    public sealed class GetMaterialPropertiesTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var material = MaterialToolHelpers.FindMaterial(ctx.GetString("materialPath", ctx.GetString("path", ctx.GetString("guid", string.Empty))));
                    if (material == null) return MaterialToolHelpers.CreateError("Material not found");

                    var shader = material.shader;
                    var count = ShaderUtil.GetPropertyCount(shader);
                    var properties = Enumerable.Range(0, count).Select(i => MaterialToolHelpers.DescribeProperty(material, i)).ToArray();

                    return new
                    {
                        success = true,
                        path = AssetDatabase.GetAssetPath(material),
                        guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material)),
                        shader = shader ? shader.name : null,
                        propertyCount = count,
                        properties
                    };
                });
            }
            catch (Exception ex)
            {
                return MaterialToolHelpers.CreateError("Failed to get material properties", ex);
            }
        }
    }

    [McpTool("unity.material.get_color", "Get a material color property")]
    public sealed class GetMaterialColorTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            return await MainThreadDispatcher.EnqueueAsync(() => MaterialToolHelpers.GetProperty(ctx, (m, p) => m.GetColor(p)));
        }
    }

    [McpTool("unity.material.get_float", "Get a material float property")]
    public sealed class GetMaterialFloatTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            return await MainThreadDispatcher.EnqueueAsync(() => MaterialToolHelpers.GetProperty(ctx, (m, p) => m.GetFloat(p)));
        }
    }

    [McpTool("unity.material.copy", "Copy material settings to another material")]
    public sealed class CopyMaterialTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            try
            {
                return await MainThreadDispatcher.EnqueueAsync(() =>
                {
                    var source = MaterialToolHelpers.FindMaterial(ctx.GetString("sourcePath", ctx.GetString("sourceGuid", string.Empty)));
                    var target = MaterialToolHelpers.FindMaterial(ctx.GetString("destinationPath", ctx.GetString("targetPath", ctx.GetString("targetGuid", string.Empty))));

                    if (source == null) return MaterialToolHelpers.CreateError("Source material not found");
                    if (target == null) return MaterialToolHelpers.CreateError("Target material not found");

                    target.CopyPropertiesFromMaterial(source);
                    target.shaderKeywords = source.shaderKeywords;
                    target.renderQueue = source.renderQueue;
                    target.enableInstancing = source.enableInstancing;
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();

                    return new
                    {
                        success = true,
                        sourcePath = AssetDatabase.GetAssetPath(source),
                        targetPath = AssetDatabase.GetAssetPath(target),
                        targetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target))
                    };
                });
            }
            catch (Exception ex)
            {
                return MaterialToolHelpers.CreateError("Failed to copy material", ex);
            }
        }
    }

    [McpTool("unity.shader.find", "Find shaders by name")]
    public sealed class FindShaderTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            return await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var query = ctx.GetString("query", ctx.GetString("name", string.Empty));
                var shaderInfos = ShaderUtil.GetAllShaderInfo();
                var shaders = shaderInfos
                    .Where(s => string.IsNullOrWhiteSpace(query) || s.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(s => new { name = s.name, supported = true })
                    .ToArray();

                return new { success = true, query, total = shaders.Length, shaders };
            });
        }
    }

    [McpTool("unity.shader.get_properties", "List shader properties")]
    public sealed class GetShaderPropertiesTool : IToolHandler
    {
        public async Task<object?> ExecuteAsync(ToolContext ctx)
        {
            return await MainThreadDispatcher.EnqueueAsync(() =>
            {
                var shaderName = ctx.GetString("shaderName", ctx.GetString("shader", ctx.GetString("name", string.Empty)));
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    return MaterialToolHelpers.CreateError($"Shader not found: {shaderName}");
                }

                var count = ShaderUtil.GetPropertyCount(shader);
                var properties = Enumerable.Range(0, count).Select(i => new
                {
                    name = ShaderUtil.GetPropertyName(shader, i),
                    displayName = ShaderUtil.GetPropertyDescription(shader, i),
                    type = ShaderUtil.GetPropertyType(shader, i).ToString(),
                    hidden = ShaderUtil.IsShaderPropertyHidden(shader, i),
                    attributes = Array.Empty<string>(),
                    flags = string.Empty
                }).ToArray();

                return new
                {
                    success = true,
                    shader = shader.name,
                    propertyCount = count,
                    properties
                };
            });
        }
    }
}
#endif
