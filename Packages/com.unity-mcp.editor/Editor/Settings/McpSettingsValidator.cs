using System;
using System.IO;

namespace UnityMCP.Editor.Settings
{
    public static class McpSettingsValidator
    {
        public static ValidationResult Validate(McpSettings settings)
        {
            var result = new ValidationResult();
            
            // Host
            if (string.IsNullOrEmpty(settings.Host))
                result.AddError("Host cannot be empty");
            
            // Port
            if (settings.Port < 1 || settings.Port > 65535)
                result.AddError($"Port must be between 1 and 65535, got {settings.Port}");
            
            // Path
            if (string.IsNullOrEmpty(settings.Path) || !settings.Path.StartsWith("/"))
                result.AddError("Path must start with /");
            
            // Server Root
            if (!string.IsNullOrEmpty(settings.ServerRootPath) && !Directory.Exists(settings.ServerRootPath))
                result.AddWarning($"Server root path does not exist: {settings.ServerRootPath}");
            
            // Build Output
            if (string.IsNullOrEmpty(settings.ServerBuildOutputPath))
                result.AddWarning("Server build output path is not set");
            
            return result;
        }
    }
    
    public class ValidationResult
    {
        public System.Collections.Generic.List<string> Errors { get; } = new();
        public System.Collections.Generic.List<string> Warnings { get; } = new();
        public bool IsValid => Errors.Count == 0;
        
        public void AddError(string message) => Errors.Add(message);
        public void AddWarning(string message) => Warnings.Add(message);
    }
}
