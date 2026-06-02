#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityMCP.Shared;

namespace UnityMCP.Editor
{
    public sealed class ToolRegistry
    {
        readonly Dictionary<string, RegisteredTool> _tools = new Dictionary<string, RegisteredTool>(StringComparer.OrdinalIgnoreCase);

        public ToolRegistry()
        {
            DiscoverTools();
        }

        public ToolManifest BuildManifest()
        {
            var manifest = new ToolManifest();

            foreach (var tool in _tools.Values.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
            {
                manifest.Tools.Add(new ToolDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    InputSchema = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = true
                    }
                });
            }

            return manifest;
        }

        public async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return CreateErrorResponse(null, -32600, "Invalid request");
            }

            try
            {
                switch (request.method)
                {
                    case "tools/list":
                        var manifestJson = ToolManifestToJson(BuildManifest());
                        return new JsonRpcResponse
                        {
                            id = request.id,
                            result = manifestJson
                        };

                    case "tools/call":
                        return await HandleToolCallAsync(request, cancellationToken).ConfigureAwait(false);

                    default:
                        return CreateErrorResponse(request.id, -32601, $"Method not found: {request.method}");
                }
            }
            catch (OperationCanceledException)
            {
                return CreateErrorResponse(request.id, -32800, "Request cancelled");
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(request.id, -32603, ex.Message);
            }
        }

        void DiscoverTools()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface)
                        continue;

                    var attribute = type.GetCustomAttribute<McpToolAttribute>();
                    if (attribute == null)
                        continue;

                    if (!typeof(IToolHandler).IsAssignableFrom(type))
                        continue;

                    if (_tools.ContainsKey(attribute.Name))
                        continue;

                    if (Activator.CreateInstance(type) is not IToolHandler handler)
                        continue;

                    _tools[attribute.Name] = new RegisteredTool(attribute.Name, attribute.Description, handler);
                }
            }
        }

        async Task<JsonRpcResponse> HandleToolCallAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            // Parse tool name and arguments from the request params
            string toolName = null;
            string argumentsJson = "{}";

            if (request.@params != null)
            {
                toolName = request.@params.name;
                argumentsJson = request.@params.arguments ?? "{}";
            }

            if (string.IsNullOrWhiteSpace(toolName))
            {
                return CreateErrorResponse(request.id, -32602, "Missing tool name");
            }

            if (!_tools.TryGetValue(toolName, out var tool))
            {
                return CreateErrorResponse(request.id, -32601, $"Tool not found: {toolName}");
            }

            var context = new ToolContext(request.id ?? string.Empty, argumentsJson, cancellationToken);
            var result = await tool.Handler.ExecuteAsync(context).ConfigureAwait(false);

            // Convert result to JSON string
            string resultJson;
            if (result is string str)
            {
                resultJson = str;
            }
            else
            {
                resultJson = SimpleJson.SerializeObject(result);
            }

            return new JsonRpcResponse
            {
                id = request.id,
                result = resultJson
            };
        }

        static JsonRpcResponse CreateErrorResponse(string id, int code, string message)
        {
            return new JsonRpcResponse
            {
                id = id,
                error = new JsonRpcError
                {
                    code = code,
                    message = message
                }
            };
        }

        static string ToolManifestToJson(ToolManifest manifest)
        {
            // Simple manual JSON serialization
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"tools\":[");
            bool first = true;
            foreach (var tool in manifest.Tools)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("{\"name\":\"");
                sb.Append(EscapeJson(tool.Name));
                sb.Append("\",\"description\":\"");
                sb.Append(EscapeJson(tool.Description));
                sb.Append("\",\"inputSchema\":{");
                sb.Append("\"type\":\"object\",\"additionalProperties\":true}");
                sb.Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        sealed class RegisteredTool
        {
            public string Name { get; }
            public string Description { get; }
            public IToolHandler Handler { get; }

            public RegisteredTool(string name, string description, IToolHandler handler)
            {
                Name = name;
                Description = description;
                Handler = handler;
            }
        }
    }
}
#endif
