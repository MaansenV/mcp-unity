#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityMCP.Shared;
using UnityMCP.Editor.Settings;
using UnityMCP.Editor.Logging;
using UnityMCP.Editor.Core;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Main plugin singleton that manages WebSocket connection and tool registry.
    /// </summary>
    [InitializeOnLoad]
    public static class UnityMcpPlugin
    {
        static WebSocketClient s_Client;
        static ToolRegistry s_Registry;
        static ConnectionState s_State;
        static bool s_Initialized;
        static McpLogBuffer s_LogBuffer;

        static UnityMcpPlugin()
        {
            Initialize();
        }

        public static ConnectionState State => s_State;
        public static WebSocketClient Client => s_Client;
        public static ToolRegistry Registry => s_Registry;
        public static McpLogBuffer LogBuffer => s_LogBuffer;
        public static event Action<ConnectionState> ConnectionStateChanged;
        public static event Action<ConnectedClientInfo> McpClientSeen;

        static void Initialize()
        {
            if (s_Initialized) return;

            s_Initialized = true;
            s_State = ConnectionState.Disconnected;
            s_LogBuffer = new McpLogBuffer(500);
            s_Registry = new ToolRegistry();
            
            var settings = McpSettingsLocator.GetOrCreateSettings();
            s_Client = new WebSocketClient(settings.WebSocketUrl, settings.AuthToken);
            s_Client.OnMessage += HandleMessage;
            s_Client.OnConnectionChanged += HandleConnectionChanged;
            s_Client.OnError += HandleConnectionError;

            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;

            s_LogBuffer.Add(McpLogLevel.Info, McpLogCategory.Connection, 
                $"Plugin initialized. WebSocket URL: {settings.WebSocketUrl}");

            if (settings.AutoConnect)
            {
                Connect();
            }
        }

        static void Shutdown()
        {
            EditorApplication.update -= Update;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            EditorApplication.quitting -= Shutdown;

            if (s_Client != null)
            {
                s_Client.OnMessage -= HandleMessage;
                s_Client.OnConnectionChanged -= HandleConnectionChanged;
                s_Client.OnError -= HandleConnectionError;
                s_Client.Dispose();
                s_Client = null;
            }

            s_State = ConnectionState.Disconnected;
            s_Initialized = false;
        }

        static void Update()
        {
            // MainThreadDispatcher already pumps its own queue via EditorApplication.update.
            // Only process WebSocket messages here to avoid double-pumping the dispatcher.
            s_Client?.ProcessMessages();
        }

        public static void Connect()
        {
            if (s_Client == null)
            {
                s_LogBuffer?.Add(McpLogLevel.Error, McpLogCategory.Connection, 
                    "Cannot connect: Client not initialized");
                return;
            }

            s_LogBuffer?.Add(McpLogLevel.Info, McpLogCategory.Connection, 
                $"Connecting to {Config.WebSocketUrl}...");
            s_Client.Connect();
        }

        public static void Disconnect()
        {
            if (s_Client == null) return;
            s_LogBuffer?.Add(McpLogLevel.Info, McpLogCategory.Connection, "Disconnecting...");
            s_Client.Disconnect();
        }

        public static void Reconnect()
        {
            s_LogBuffer?.Add(McpLogLevel.Info, McpLogCategory.Connection, "Reconnecting...");
            Disconnect();
            Connect();
        }

        public static void ReloadSettings()
        {
            Config.ReloadSettings();
            var settings = McpSettingsLocator.GetOrCreateSettings();
            
            if (s_Client != null)
            {
                s_Client.Dispose();
                s_Client = new WebSocketClient(settings.WebSocketUrl, settings.AuthToken);
                s_Client.OnMessage += HandleMessage;
                s_Client.OnConnectionChanged += HandleConnectionChanged;
                s_Client.OnError += HandleConnectionError;
            }
            
            s_LogBuffer?.Add(McpLogLevel.Info, McpLogCategory.Settings, 
                $"Settings reloaded. New WebSocket URL: {settings.WebSocketUrl}");
        }

        static void HandleConnectionChanged(ConnectionState state)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                s_State = state;
                ConnectionStateChanged?.Invoke(state);
                s_LogBuffer?.Add(McpLogLevel.Info, McpLogCategory.Connection, 
                    $"Connection state changed: {state}");
            });
        }

        static void HandleConnectionError(string message)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                s_LogBuffer?.Add(McpLogLevel.Error, McpLogCategory.Connection, message);
            });
        }

        static void HandleMessage(string message)
        {
            _ = HandleMessageAsync(message);
        }

        static async Task HandleMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || s_Client == null || s_Registry == null)
                return;

            JsonRpcRequest request;
            try
            {
                request = JsonRpcParser.ParseRequest(message);
            }
            catch (Exception ex)
            {
                MainThreadDispatcher.Enqueue(() => 
                {
                    s_LogBuffer?.Add(McpLogLevel.Error, McpLogCategory.Connection, 
                        $"Failed to parse message: {ex.Message}");
                });
                return;
            }

            if (request == null) return;

            if (request.method == "mcp/client_seen")
            {
                HandleMcpClientSeenNotification(message);
                return;
            }

            if (request.method != "tools/call" && request.method != "tools/list")
                return;

            try
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    s_LogBuffer?.Add(McpLogLevel.Debug, McpLogCategory.Tool,
                        $"[DEBUG-MCP] Received {request.method} id={request.id} tool={request.@params?.name}");
                });

                var response = await s_Registry.HandleRequestAsync(request).ConfigureAwait(false);
                if (response != null && !string.IsNullOrEmpty(request.id))
                {
                    await s_Client.SendJsonRpcResponseAsync(response).ConfigureAwait(false);
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        s_LogBuffer?.Add(McpLogLevel.Debug, McpLogCategory.Tool,
                            $"[DEBUG-MCP] Sent response id={request.id} tool={request.@params?.name}");
                    });
                }
            }
            catch (Exception ex)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    s_LogBuffer?.Add(McpLogLevel.Error, McpLogCategory.Tool,
                        $"[DEBUG-MCP] Failed handling {request.method} id={request.id} tool={request.@params?.name}: {ex.GetType().Name}: {ex.Message}");
                });

                if (!string.IsNullOrEmpty(request.id))
                {
                    try
                    {
                        await s_Client.SendJsonRpcResponseAsync(new JsonRpcResponse
                        {
                            id = request.id,
                            error = new JsonRpcError
                            {
                                code = -32603,
                                message = ex.Message
                            }
                        }).ConfigureAwait(false);
                    }
                    catch { }
                }
            }
        }

        static void HandleMcpClientSeenNotification(string message)
        {
            var paramsJson = SimpleJson.GetRawObject(message, "params") ?? "{}";
            var id = SimpleJson.GetString(paramsJson, "id", "mcp-stdio-client");
            var name = SimpleJson.GetString(paramsJson, "name", "MCP stdio client");
            var remoteAddress = SimpleJson.GetString(paramsJson, "remoteAddress", "stdio");
            var toolName = SimpleJson.GetString(paramsJson, "tool", string.Empty);

            if (!string.IsNullOrWhiteSpace(toolName))
            {
                remoteAddress = string.IsNullOrWhiteSpace(remoteAddress)
                    ? $"last tool: {toolName}"
                    : $"{remoteAddress} • last tool: {toolName}";
            }

            MainThreadDispatcher.Enqueue(() =>
            {
                McpClientSeen?.Invoke(new ConnectedClientInfo(id, name, remoteAddress));
                s_LogBuffer?.Add(McpLogLevel.Debug, McpLogCategory.Connection,
                    $"MCP client active: {name} ({remoteAddress})");
            });
        }
    }

    /// <summary>
    /// Simple JSON-RPC parser without System.Text.Json dependency.
    /// </summary>
    internal static class JsonRpcParser
    {
        public static JsonRpcRequest ParseRequest(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            var request = new JsonRpcRequest();
            request.jsonrpc = SimpleJson.GetString(json, "jsonrpc", "2.0");
            request.id = SimpleJson.GetString(json, "id", "");
            request.method = SimpleJson.GetString(json, "method", "");

            // Parse params
            var paramsJson = SimpleJson.GetRawObject(json, "params");
            if (paramsJson != null)
            {
                request.@params = new JsonRpcRequestParams
                {
                    name = SimpleJson.GetString(paramsJson, "name", ""),
                    arguments = SimpleJson.GetRawObject(paramsJson, "arguments") ?? "{}"
                };
            }

            return request;
        }

        public static string ResponseToJson(JsonRpcResponse response)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"jsonrpc\":\"2.0\"");
            sb.Append(",\"id\":\"");
            sb.Append(Escape(response.id ?? ""));
            sb.Append("\"");

            if (response.error != null)
            {
                sb.Append(",\"error\":{\"code\":");
                sb.Append(response.error.code);
                sb.Append(",\"message\":\"");
                sb.Append(Escape(response.error.message));
                sb.Append("\"}");
            }
            else if (response.result != null)
            {
                sb.Append(",\"result\":");
                sb.Append(response.result);
            }

            sb.Append("}");
            return sb.ToString();
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }
    }
}
#endif
