#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityMCP.Editor;
using UnityMCP.Shared;

namespace UnityMCP.Editor.Tools
{
    static class ScriptToolHelpers
    {
        public static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        }

        /// <summary>
        /// Validates that a script path is safe and inside the Assets folder.
        /// Returns a normalised absolute disk path for File.WriteAllText / File.ReadAllText.
        /// </summary>
        public static bool TryValidateScriptPath(string rawPath, out string normalizedAssetPath, out string absolutePath, out string error)
        {
            normalizedAssetPath = null;
            absolutePath = null;
            error = null;

            if (!AssetPathValidator.TryValidateAssetPath(rawPath, out normalizedAssetPath, out error))
                return false;

            // Convert validated asset path to absolute disk path for File APIs
            absolutePath = Path.Combine(UnityMCP.Editor.Settings.McpPathUtility.GetProjectRoot(),
                normalizedAssetPath.Replace('/', Path.DirectorySeparatorChar));
            return true;
        }

        public static void EnsureAssetDirectory(string assetPath)
        {
            assetPath = NormalizePath(assetPath);
            var dir = NormalizePath(Path.GetDirectoryName(assetPath) ?? string.Empty);
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir))
            {
                return;
            }

            var parts = dir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            var current = parts[0];
            if (!AssetDatabase.IsValidFolder(current) && !string.Equals(current, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return;
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

        public static string GenerateDefaultScript(string name)
        {
            return $@"using UnityEngine;

public class {name} : MonoBehaviour
{{
    void Start()
    {{
        
    }}
    
    void Update()
    {{
        
    }}
}}
";
        }

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

        public static string[] GetCompilerMessages()
        {
            var methods = typeof(CompilationPipeline).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => string.Equals(m.Name, "GetCompilerMessages", StringComparison.Ordinal))
                .ToArray();

            foreach (var method in methods)
            {
                try
                {
                    var parameters = method.GetParameters();
                    object result = null;

                    if (parameters.Length == 0)
                    {
                        result = method.Invoke(null, null);
                    }
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        result = method.Invoke(null, new object[] { string.Empty });
                    }

                    if (result is IEnumerable<CompilerMessage> messages)
                    {
                        return messages.Select(m => m.message).Where(m => !string.IsNullOrWhiteSpace(m)).ToArray();
                    }
                }
                catch
                {
                    // Ignore and continue to the next overload.
                }
            }

            return Array.Empty<string>();
        }

        public static bool TryBuildRoslynAssembly(string code, out byte[] assemblyBytes, out string error)
        {
            assemblyBytes = null;
            error = null;

            var codeAnalysisAssembly = Type.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation, Microsoft.CodeAnalysis.CSharp")?.Assembly
                ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation") != null);

            if (codeAnalysisAssembly == null)
            {
                error = "Roslyn (Microsoft.CodeAnalysis.CSharp) is not available in this editor instance.";
                return false;
            }

            var syntaxTreeType = Type.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree, Microsoft.CodeAnalysis.CSharp")
                ?? codeAnalysisAssembly.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree");
            var compilationType = Type.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation, Microsoft.CodeAnalysis.CSharp")
                ?? codeAnalysisAssembly.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation");
            var metadataReferenceType = Type.GetType("Microsoft.CodeAnalysis.MetadataReference, Microsoft.CodeAnalysis")
                ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Microsoft.CodeAnalysis.MetadataReference")).FirstOrDefault(t => t != null);
            var outputKindType = Type.GetType("Microsoft.CodeAnalysis.OutputKind, Microsoft.CodeAnalysis")
                ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Microsoft.CodeAnalysis.OutputKind")).FirstOrDefault(t => t != null);
            var syntaxFactoryType = Type.GetType("Microsoft.CodeAnalysis.CSharp.SyntaxFactory, Microsoft.CodeAnalysis.CSharp")
                ?? codeAnalysisAssembly.GetType("Microsoft.CodeAnalysis.CSharp.SyntaxFactory");
            var optionsType = Type.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions, Microsoft.CodeAnalysis.CSharp")
                ?? codeAnalysisAssembly.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions");

            if (syntaxTreeType == null || compilationType == null || syntaxFactoryType == null || metadataReferenceType == null || outputKindType == null || optionsType == null)
            {
                error = "Roslyn types could not be resolved.";
                return false;
            }

            var wrapperCode = $@"using System;
public static class McpRuntimeScript
{{
    public static object Execute()
    {{
{Indent(code, 8)}
    }}
}}";

            try
            {
                var parseText = syntaxTreeType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "ParseText" && m.GetParameters().Length >= 1);

                object syntaxTree;
                var parseArgs = parseText.GetParameters().Length switch
                {
                    1 => new object[] { wrapperCode },
                    2 => new object[] { wrapperCode, null },
                    _ => new object[] { wrapperCode }
                };
                syntaxTree = parseText.Invoke(null, parseArgs);

                var createFromFile = metadataReferenceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "CreateFromFile" && m.GetParameters().Length == 1);

                var references = new List<object>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic)
                    {
                        continue;
                    }

                    var location = string.Empty;
                    try
                    {
                        location = assembly.Location;
                    }
                    catch
                    {
                        location = string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(location) || !File.Exists(location))
                    {
                        continue;
                    }

                    references.Add(createFromFile.Invoke(null, new object[] { location }));
                }

                var syntaxTrees = Array.CreateInstance(syntaxTreeType, 1);
                syntaxTrees.SetValue(syntaxTree, 0);

                var metadataReferenceArray = Array.CreateInstance(metadataReferenceType, references.Count);
                for (var i = 0; i < references.Count; i++)
                {
                    metadataReferenceArray.SetValue(references[i], i);
                }

                object outputKind = Enum.Parse(outputKindType, "DynamicallyLinkedLibrary");

                var parseOptions = syntaxTree.GetType().GetProperty("Options")?.GetValue(syntaxTree);
                object compilationOptions = null;
                if (optionsType != null)
                {
                    var ctor = optionsType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length >= 1);
                    if (ctor != null)
                    {
                        var ctorParameters = ctor.GetParameters();
                        var args = new object[ctorParameters.Length];
                        args[0] = outputKind;
                        for (var i = 1; i < args.Length; i++)
                        {
                            args[i] = Type.Missing;
                        }
                        compilationOptions = ctor.Invoke(args);
                    }
                }

                var createMethod = compilationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "Create" && m.GetParameters().Length >= 4);

                var createParameters = createMethod.GetParameters();
                var createArgs = new object[createParameters.Length];
                createArgs[0] = "McpRuntimeScriptAssembly";
                createArgs[1] = syntaxTrees;
                createArgs[2] = metadataReferenceArray;
                createArgs[3] = compilationOptions;
                for (var i = 4; i < createArgs.Length; i++)
                {
                    createArgs[i] = Type.Missing;
                }

                var compilation = createMethod.Invoke(null, createArgs);
                if (compilation == null)
                {
                    error = "Failed to create Roslyn compilation.";
                    return false;
                }

                using var peStream = new MemoryStream();
                using var pdbStream = new MemoryStream();

                var emitMethod = compilation.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m => m.Name == "Emit" && m.GetParameters().Length >= 1);
                var emitResult = emitMethod.Invoke(compilation, new object[] { peStream });

                var successProp = emitResult?.GetType().GetProperty("Success");
                if (successProp == null || !(successProp.GetValue(emitResult) is bool success) || !success)
                {
                    var diagnostics = emitResult?.GetType().GetProperty("Diagnostics")?.GetValue(emitResult) as System.Collections.IEnumerable;
                    var messages = new List<string>();
                    if (diagnostics != null)
                    {
                        foreach (var diagnostic in diagnostics)
                        {
                            messages.Add(diagnostic.ToString());
                        }
                    }

                    error = string.Join(Environment.NewLine, messages);
                    if (string.IsNullOrWhiteSpace(error))
                    {
                        error = "Roslyn compilation failed.";
                    }

                    return false;
                }

                assemblyBytes = peStream.ToArray();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static string Indent(string text, int spaces)
        {
            var pad = new string(' ', spaces);
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            return string.Join(Environment.NewLine, lines.Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : pad + line));
        }

        public static bool IsCompiling()
        {
            return EditorApplication.isCompiling;
        }
    }

    [McpTool("unity.script.create", "Create a new C# script")]
    internal sealed class ScriptCreateTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var name = ScriptToolHelpers.Get(context, "name", string.Empty);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return new { success = false, error = "Missing script name" };
                }

                var content = ScriptToolHelpers.Get<string>(context, "content", null);
                var rawPath = ScriptToolHelpers.Get(context, "path", $"Assets/Scripts/{name}.cs");

                if (!ScriptToolHelpers.TryValidateScriptPath(rawPath, out var assetPath, out var absolutePath, out var error))
                    return new { success = false, error };

                ScriptToolHelpers.EnsureAssetDirectory(assetPath);
                if (string.IsNullOrEmpty(content))
                {
                    content = ScriptToolHelpers.GenerateDefaultScript(name);
                }

                File.WriteAllText(absolutePath, content, new UTF8Encoding(false));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                return new
                {
                    success = true,
                    path = assetPath,
                    name,
                    compiling = ScriptToolHelpers.IsCompiling()
                };
            });
        }
    }

    [McpTool("unity.script.update", "Modify script content")]
    internal sealed class ScriptUpdateTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var rawPath = ScriptToolHelpers.Get(context, "path", string.Empty);
                var content = ScriptToolHelpers.Get(context, "content", string.Empty);

                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    return new { success = false, error = "Missing path" };
                }

                if (!ScriptToolHelpers.TryValidateScriptPath(rawPath, out var assetPath, out var absolutePath, out var error))
                    return new { success = false, error };

                if (!File.Exists(absolutePath))
                {
                    return new { success = false, error = $"Script not found: {assetPath}" };
                }

                File.WriteAllText(absolutePath, content ?? string.Empty, new UTF8Encoding(false));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                return new
                {
                    success = true,
                    path = assetPath,
                    compiling = ScriptToolHelpers.IsCompiling()
                };
            });
        }
    }

    [McpTool("unity.script.delete", "Delete a script file")]
    internal sealed class ScriptDeleteTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var rawPath = ScriptToolHelpers.Get(context, "path", string.Empty);
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    return new { success = false, error = "Missing path" };
                }

                if (!ScriptToolHelpers.TryValidateScriptPath(rawPath, out var assetPath, out var absolutePath, out var error))
                    return new { success = false, error };

                if (!File.Exists(absolutePath) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null)
                {
                    return new { success = false, error = $"Script not found: {assetPath}" };
                }

                var deleted = AssetDatabase.DeleteAsset(assetPath);
                if (!deleted && File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                    var metaPath = absolutePath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }

                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    deleted = true;
                }

                return new
                {
                    success = deleted,
                    path = assetPath,
                    compiling = ScriptToolHelpers.IsCompiling()
                };
            });
        }
    }

    [McpTool("unity.script.get_content", "Read script file")]
    internal sealed class ScriptGetContentTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var rawPath = ScriptToolHelpers.Get(context, "path", string.Empty);
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    return new { success = false, error = "Missing path" };
                }

                if (!ScriptToolHelpers.TryValidateScriptPath(rawPath, out var assetPath, out var absolutePath, out var error))
                    return new { success = false, error };

                if (!File.Exists(absolutePath))
                {
                    return new { success = false, error = $"Script not found: {assetPath}" };
                }

                return new
                {
                    success = true,
                    path = assetPath,
                    content = File.ReadAllText(absolutePath)
                };
            });
        }
    }

    [McpTool("unity.script.compile_status", "Check if compiling")]
    internal sealed class ScriptCompileStatusTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                return new
                {
                    success = true,
                    isCompiling = ScriptToolHelpers.IsCompiling(),
                    isUpdating = EditorApplication.isUpdating,
                    editorIsCompiling = EditorApplication.isCompiling
                };
            });
        }
    }

    [McpTool("unity.script.wait_for_compilation", "Wait for compile to finish")]
    internal sealed class ScriptWaitForCompilationTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            var timeoutSeconds = ScriptToolHelpers.Get(context, "timeoutSeconds", 120f);
            return WaitAndSummarizeAsync(TimeSpan.FromSeconds(Math.Max(1f, timeoutSeconds)), context.CancellationToken);
        }

        static async Task<object?> WaitAndSummarizeAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var waitTask = await MainThreadDispatcher.RunAsync(() => WaitForCompilationAsync(timeout, cancellationToken)).ConfigureAwait(false);
            var completed = await waitTask.ConfigureAwait(false);
            return new
            {
                success = completed,
                timedOut = !completed,
                isCompiling = ScriptToolHelpers.IsCompiling()
            };
        }

        static Task<bool> WaitForCompilationAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!ScriptToolHelpers.IsCompiling())
            {
                return Task.FromResult(true);
            }

            var tcs = new TaskCompletionSource<bool>();
            var started = DateTime.UtcNow;

            void Poll()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    EditorApplication.update -= Poll;
                    tcs.TrySetCanceled(cancellationToken);
                    return;
                }

                if (!ScriptToolHelpers.IsCompiling())
                {
                    EditorApplication.update -= Poll;
                    tcs.TrySetResult(true);
                    return;
                }

                if (DateTime.UtcNow - started >= timeout)
                {
                    EditorApplication.update -= Poll;
                    tcs.TrySetResult(false);
                }
            }

            EditorApplication.update += Poll;
            return tcs.Task;
        }
    }

    [McpTool("unity.script.get_errors", "Get compilation errors")]
    internal sealed class ScriptGetErrorsTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var messages = ScriptToolHelpers.GetCompilerMessages();
                return new
                {
                    success = true,
                    isCompiling = ScriptToolHelpers.IsCompiling(),
                    messages,
                    errors = messages.Where(m => !string.IsNullOrWhiteSpace(m)).ToArray()
                };
            });
        }
    }

    [McpTool("unity.script.execute_code", "Execute C# code via Roslyn if available")]
    internal sealed class ScriptExecuteCodeTool : IToolHandler
    {
        public Task<object?> ExecuteAsync(ToolContext context)
        {
            return MainThreadDispatcher.RunAsync<object?>(() =>
            {
                var code = ScriptToolHelpers.Get(context, "code", string.Empty);
                if (string.IsNullOrWhiteSpace(code))
                {
                    return new { success = false, error = "Missing code" };
                }

                if (!ScriptToolHelpers.TryBuildRoslynAssembly(code, out var assemblyBytes, out var error))
                {
                    return new { success = false, error };
                }

                try
                {
                    var assembly = System.Reflection.Assembly.Load(assemblyBytes);
                    var type = assembly.GetType("McpRuntimeScript");
                    var method = type?.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                    var result = method?.Invoke(null, null);

                    return new
                    {
                        success = true,
                        result = result?.ToString() ?? string.Empty
                    };
                }
                catch (Exception ex)
                {
                    return new { success = false, error = ex.Message };
                }
            });
        }
    }
}
#endif
