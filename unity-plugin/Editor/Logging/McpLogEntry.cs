using System;

namespace UnityMCP.Editor.Logging
{
    [Serializable]
    public class McpLogEntry
    {
        public DateTime Timestamp;
        public McpLogLevel Level;
        public McpLogCategory Category;
        public string Message;
        public string Details;
        
        public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] [{Category}] {Message}";
        
        public McpLogEntry(McpLogLevel level, McpLogCategory category, string message, string details = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Category = category;
            Message = message;
            Details = details;
        }
    }
}
