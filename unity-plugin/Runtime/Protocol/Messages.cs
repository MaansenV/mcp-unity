namespace UnityMCP.Shared
{
    /// <summary>
    /// JSON-RPC 2.0 request from Go MCP server to Unity.
    /// </summary>
    public sealed class JsonRpcRequest
    {
        public string jsonrpc { get; set; } = "2.0";
        public string id { get; set; } = string.Empty;
        public string method { get; set; } = string.Empty;
        public JsonRpcRequestParams @params { get; set; }
    }

    public sealed class JsonRpcRequestParams
    {
        public string name { get; set; } = string.Empty;
        public string arguments { get; set; } = "{}"; // Raw JSON string
    }

    /// <summary>
    /// JSON-RPC 2.0 notification (no id, no response expected).
    /// </summary>
    public sealed class JsonRpcNotification
    {
        public string jsonrpc { get; set; } = "2.0";
        public string method { get; set; } = string.Empty;
        public string @params { get; set; } = "{}"; // Raw JSON string
    }

    /// <summary>
    /// JSON-RPC 2.0 response from Unity to Go MCP server.
    /// </summary>
    public sealed class JsonRpcResponse
    {
        public string jsonrpc { get; set; } = "2.0";
        public string id { get; set; } = string.Empty;
        public string result { get; set; } // Raw JSON string
        public JsonRpcError error { get; set; }
    }

    public sealed class JsonRpcError
    {
        public int code { get; set; }
        public string message { get; set; } = string.Empty;
        public string data { get; set; } // Raw JSON string
    }

    /// <summary>
    /// Tool call parameters parsed from JSON-RPC request.
    /// </summary>
    public sealed class ToolCallParams
    {
        public string Name { get; set; } = string.Empty;
        public string ArgumentsJson { get; set; } = "{}";
    }

    /// <summary>
    /// Result from a tool execution.
    /// </summary>
    public sealed class ToolCallResult
    {
        public bool Success { get; set; }
        public string ContentJson { get; set; } = "{}";
        public string Error { get; set; }
    }
}
