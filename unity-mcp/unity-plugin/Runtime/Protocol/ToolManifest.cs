using System.Collections.Generic;

namespace UnityMCP.Shared
{
    public sealed class ToolManifest
    {
        public string Version { get; set; } = "1.0";
        public List<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();
    }

    public sealed class ToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object?> InputSchema { get; set; } = new Dictionary<string, object?>();
    }
}
