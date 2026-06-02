using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityMCP.Editor.Settings;

namespace UnityMCP.Editor.Services
{
    public class OpencodeExportResult
    {
        public bool Success;
        public string Path;
        public string Error;
    }
    
    public static class OpencodeConfigService
    {
        // ----------------------------------------------------------------
        // Global OpenCode config path (standard installation)
        // ----------------------------------------------------------------
        
        /// <summary>
        /// Returns the standard OpenCode global config path: ~/.config/opencode/opencode.json
        /// Works cross-platform (Windows, macOS, Linux).
        /// </summary>
        public static string GetGlobalConfigPath()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".config", "opencode", "opencode.json");
        }
        
        /// <summary>
        /// Checks if the Unity MCP server is already configured in the global OpenCode config.
        /// </summary>
        public static (bool exists, string path) CheckGlobalConfig()
        {
            var path = GetGlobalConfigPath();
            if (!File.Exists(path))
                return (false, path);
            
            try
            {
                var json = File.ReadAllText(path);
                return (json.Contains("\"unity\""), path);
            }
            catch
            {
                return (false, path);
            }
        }
        
        // ----------------------------------------------------------------
        // Project-local export (existing)
        // ----------------------------------------------------------------
        
        public static async Task<OpencodeExportResult> ExportAsync(McpSettings settings, CancellationToken ct = default)
        {
            var result = new OpencodeExportResult();
            
            try
            {
                var json = GenerateJson(settings);
                
                // Backup existing file
                if (File.Exists(settings.OpencodeConfigPath))
                {
                    var backupPath = settings.OpencodeConfigPath + $".bak-{DateTime.Now:yyyyMMdd-HHmmss}";
                    File.Copy(settings.OpencodeConfigPath, backupPath, true);
                    Debug.Log($"[UnityMCP] Backed up existing config to: {backupPath}");
                }
                
                // Ensure directory exists
                var dir = Path.GetDirectoryName(settings.OpencodeConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                await File.WriteAllTextAsync(settings.OpencodeConfigPath, json, ct);
                
                result.Success = true;
                result.Path = settings.OpencodeConfigPath;
                
                Debug.Log($"[UnityMCP] Exported opencode.json to: {settings.OpencodeConfigPath}");
            }
            catch (System.Exception ex)
            {
                result.Error = $"Export failed: {ex.Message}";
            }
            
            return result;
        }
        
        public static string GenerateJson(McpSettings settings)
        {
            // Escape paths for JSON
            var serverPath = settings.ServerBuildOutputPath.Replace("\\", "/");
            var projectPath = Directory.GetCurrentDirectory().Replace("\\", "/");
            
            return $"{{\n" +
                   $"  \"$schema\": \"https://opencode.ai/config.json\",\n" +
                   $"  \"mcp\": {{\n" +
                   $"    \"unity\": {{\n" +
                   $"      \"type\": \"local\",\n" +
                   $"      \"command\": [\"{serverPath}\"],\n" +
                   $"      \"enabled\": true,\n" +
                   $"      \"environment\": {{\n" +
                   $"        \"UNITY_MCP_WS_HOST\": \"{settings.Host}\",\n" +
                   $"        \"UNITY_MCP_WS_PORT\": \"{settings.Port}\",\n" +
                   $"        \"UNITY_MCP_WS_PATH\": \"{settings.Path}\",\n" +
                   $"        \"UNITY_MCP_PROJECT_PATH\": \"{projectPath}\"\n" +
                   $"      }},\n" +
                   $"      \"timeout\": 10000\n" +
                   $"    }}\n" +
                   $"  }}\n" +
                   $"}}";
        }
        
        // ----------------------------------------------------------------
        // Global OpenCode MCP+ auto-configure
        // ----------------------------------------------------------------
        
        /// <summary>
        /// Merges the Unity MCP server entry into the global OpenCode config (~/.config/opencode/opencode.json).
        /// Preserves all existing settings (plugins, agents, providers, other MCP servers).
        /// </summary>
        public static async Task<OpencodeExportResult> ConfigureGlobalAsync(McpSettings settings, CancellationToken ct = default)
        {
            var result = new OpencodeExportResult();
            var globalPath = GetGlobalConfigPath();
            
            try
            {
                var dir = Path.GetDirectoryName(globalPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                // Backup existing file
                if (File.Exists(globalPath))
                {
                    var backupPath = globalPath + $".bak-unitymcp-{DateTime.Now:yyyyMMdd-HHmmss}";
                    File.Copy(globalPath, backupPath, true);
                    Debug.Log($"[UnityMCP] Backed up global OpenCode config to: {backupPath}");
                }
                
                // Read existing config or start with empty
                string existingJson = "{}";
                if (File.Exists(globalPath))
                    existingJson = await File.ReadAllTextAsync(globalPath, ct);
                
                // Merge Unity MCP entry into existing JSON
                var mergedJson = MergeUnityMcpEntry(existingJson, settings);
                
                await File.WriteAllTextAsync(globalPath, mergedJson, ct);
                
                result.Success = true;
                result.Path = globalPath;
                
                Debug.Log($"[UnityMCP] Configured OpenCode global config: {globalPath}");
            }
            catch (System.Exception ex)
            {
                result.Error = $"Global config failed: {ex.Message}";
            }
            
            return result;
        }
        
        /// <summary>
        /// Merges the Unity MCP entry into an existing JSON string.
        /// Handles: file doesn't exist, no mcp section, mcp exists but no unity, unity already exists.
        /// </summary>
        internal static string MergeUnityMcpEntry(string existingJson, McpSettings settings)
        {
            var entry = GenerateUnityMcpEntry(settings);
            
            // Case 1: Empty or whitespace - create new config
            if (string.IsNullOrWhiteSpace(existingJson) || existingJson.Trim() == "{}")
            {
                return "{\n" +
                       "  \"$schema\": \"https://opencode.ai/config.json\",\n" +
                       "  \"mcp\": {\n" +
                       "    \"unity\": " + FormatJsonEntry(entry) + "\n" +
                       "  }\n" +
                       "}";
            }
            
            // Case 2: "mcp" key doesn't exist yet - add it
            var mcpIndex = FindTopLevelKey(existingJson, "mcp");
            if (mcpIndex < 0)
            {
                // Insert "mcp" section before the last }
                var lastBrace = existingJson.LastIndexOf('}');
                if (lastBrace < 0)
                    lastBrace = existingJson.Length;
                
                var insert = "\"mcp\": {\n    \"unity\": " + FormatJsonEntry(entry) + "\n  }";
                
                // Check if we need a comma
                var beforeBrace = existingJson.Substring(0, lastBrace).TrimEnd();
                var needsComma = beforeBrace.Length > 0 && !beforeBrace.EndsWith("{") && !beforeBrace.EndsWith(",") && !beforeBrace.EndsWith("[");
                
                var sb = new StringBuilder(existingJson);
                if (needsComma)
                    sb.Insert(lastBrace, ",\n  " + insert);
                else
                    sb.Insert(lastBrace, "\n  " + insert);
                
                return sb.ToString();
            }
            
            // Case 3: "mcp" section exists - find or add "unity" inside it
            var mcpSectionStart = existingJson.IndexOf('{', mcpIndex);
            if (mcpSectionStart < 0)
                return existingJson; // Malformed JSON, don't touch
            
            var mcpSectionEnd = FindMatchingBrace(existingJson, mcpSectionStart);
            if (mcpSectionEnd < 0)
                return existingJson;
            
            var mcpContent = existingJson.Substring(mcpSectionStart + 1, mcpSectionEnd - mcpSectionStart - 1);
            
            // Check if "unity" already exists inside mcp section
            var unityKeyIndex = FindNestedKey(mcpContent, "unity");
            if (unityKeyIndex >= 0)
            {
                // Replace existing unity entry
                var keyStart = mcpContent.IndexOf("unity", unityKeyIndex);
                var valueStart = mcpContent.IndexOf(':', keyStart + 5) + 1;
                var valueEnd = FindValueEnd(mcpContent, valueStart);
                
                if (valueEnd > valueStart)
                {
                    var newMcpContent = mcpContent.Substring(0, valueStart) + "\n    " + FormatJsonEntry(entry) + "\n  " + mcpContent.Substring(valueEnd);
                    return existingJson.Substring(0, mcpSectionStart + 1) + newMcpContent + existingJson.Substring(mcpSectionEnd);
                }
            }
            
            // Add unity to existing mcp section
            var insertPos = mcpSectionEnd;
            var trimmed = mcpContent.TrimEnd();
            var needsComma2 = trimmed.Length > 0 && !trimmed.EndsWith("{") && !trimmed.EndsWith(",") && !trimmed.EndsWith("[");
            
            var addition = (needsComma2 ? ",\n    " : "\n    ") + "\"unity\": " + FormatJsonEntry(entry);
            
            var sb2 = new StringBuilder(existingJson);
            sb2.Insert(insertPos, addition);
            
            return sb2.ToString();
        }
        
        /// <summary>
        /// Generates just the Unity MCP server entry JSON object (without the "unity" key wrapper).
        /// </summary>
        public static string GenerateUnityMcpEntry(McpSettings settings)
        {
            var serverPath = settings.ServerBuildOutputPath.Replace("\\", "/");
            var projectPath = Directory.GetCurrentDirectory().Replace("\\", "/");
            
            return "{\n" +
                   "      \"type\": \"local\",\n" +
                   $"      \"command\": [\"{serverPath}\"],\n" +
                   "      \"enabled\": true,\n" +
                   "      \"environment\": {\n" +
                   $"        \"UNITY_MCP_WS_HOST\": \"{settings.Host}\",\n" +
                   $"        \"UNITY_MCP_WS_PORT\": \"{settings.Port}\",\n" +
                   $"        \"UNITY_MCP_WS_PATH\": \"{settings.Path}\",\n" +
                   $"        \"UNITY_MCP_PROJECT_PATH\": \"{projectPath}\"\n" +
                   "      },\n" +
                   "      \"timeout\": 10000\n" +
                   "    }";
        }
        
        // ----------------------------------------------------------------
        // Validation
        // ----------------------------------------------------------------
        
        public static bool ValidateExistingConfig(string path, out string error)
        {
            error = null;
            
            if (!File.Exists(path))
            {
                error = "Config file does not exist";
                return false;
            }
            
            try
            {
                var json = File.ReadAllText(path);
                // Basic validation - check for valid JSON structure
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                {
                    error = "Config file is not valid JSON";
                    return false;
                }
                return true;
            }
            catch (System.Exception ex)
            {
                error = $"Error reading config: {ex.Message}";
                return false;
            }
        }
        
        // ----------------------------------------------------------------
        // Internal JSON helpers (no external dependencies)
        // ----------------------------------------------------------------
        
        private static string FormatJsonEntry(string entry)
        {
            // Ensure consistent formatting with 4-space indent for nesting
            return entry.Trim();
        }
        
        /// <summary>
        /// Finds a top-level JSON key (not nested inside braces).
        /// Returns the index of the first character of the key, or -1 if not found.
        /// </summary>
        private static int FindTopLevelKey(string json, string key)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            
            for (int i = 0; i < json.Length - key.Length; i++)
            {
                var c = json[i];
                
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                
                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }
                
                if (inString) continue;
                
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                
                if (depth == 0 && json.Substring(i, key.Length + 1) == "\"" + key + "\"")
                    return i + 1; // Return position after opening quote
            }
            
            return -1;
        }
        
        /// <summary>
        /// Finds a key within the current nesting level only.
        /// </summary>
        private static int FindNestedKey(string json, string key)
        {
            return FindTopLevelKey(json, key);
        }
        
        /// <summary>
        /// Finds the matching closing brace for an opening brace at the given position.
        /// </summary>
        private static int FindMatchingBrace(string json, int openBraceIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            
            for (int i = openBraceIndex; i < json.Length; i++)
            {
                var c = json[i];
                
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                
                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }
                
                if (inString) continue;
                
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            
            return -1;
        }
        
        /// <summary>
        /// Finds the end of a JSON value starting at the given position.
        /// Handles strings, objects, arrays, numbers, booleans, and null.
        /// </summary>
        private static int FindValueEnd(string json, int startIndex)
        {
            // Skip whitespace
            while (startIndex < json.Length && char.IsWhiteSpace(json[startIndex]))
                startIndex++;
            
            if (startIndex >= json.Length) return startIndex;
            
            var c = json[startIndex];
            
            if (c == '"')
            {
                // String - find closing quote
                bool escape = false;
                for (int i = startIndex + 1; i < json.Length; i++)
                {
                    if (escape) { escape = false; continue; }
                    if (json[i] == '\\') { escape = true; continue; }
                    if (json[i] == '"') return i + 1;
                }
            }
            else if (c == '{')
            {
                var end = FindMatchingBrace(json, startIndex);
                return end >= 0 ? end + 1 : startIndex + 1;
            }
            else if (c == '[')
            {
                var end = FindMatchingBracket(json, startIndex);
                return end >= 0 ? end + 1 : startIndex + 1;
            }
            else
            {
                // Primitive (number, bool, null) - read until comma, brace, or bracket
                for (int i = startIndex; i < json.Length; i++)
                {
                    if (json[i] == ',' || json[i] == '}' || json[i] == ']')
                        return i;
                }
            }
            
            return json.Length;
        }
        
        private static int FindMatchingBracket(string json, int openBracketIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            
            for (int i = openBracketIndex; i < json.Length; i++)
            {
                var c = json[i];
                
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                
                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }
                
                if (inString) continue;
                
                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            
            return -1;
        }
    }
}
