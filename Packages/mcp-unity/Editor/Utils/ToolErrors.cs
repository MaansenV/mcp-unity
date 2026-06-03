namespace McpUnity.Utils
{
    /// <summary>
    /// Structured error messages for MCP tools. Provides consistent, well-formatted
    /// error responses that help LLMs understand and recover from failures.
    /// </summary>
    public static class ToolErrors
    {
        public static string NotFound(string what, string identifier = null)
        {
            return identifier != null
                ? $"{what} not found: '{identifier}'. Check the name/path/ID and try again."
                : $"{what} not found.";
        }

        public static string InvalidInput(string message)
        {
            return $"Invalid input: {message}";
        }

        public static string ExecutionError(string operation, string message)
        {
            return $"Failed to {operation}: {message}";
        }

        public static string MultipleFound(string what, int count, string suggestion = null)
        {
            var msg = $"Multiple {what}s found ({count}).";
            if (suggestion != null) msg += $" {suggestion}";
            return msg;
        }
    }
}
