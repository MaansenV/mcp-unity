using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityMCP.Editor.Settings;
using UnityMCP.Shared;

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
        /// Uses SimpleJson for structural lookup instead of fragile substring matching.
        /// </summary>
        public static (bool exists, string path) CheckGlobalConfig()
        {
            var path = GetGlobalConfigPath();
            if (!File.Exists(path))
                return (false, path);
            
            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return (false, path);

                // Check for "mcp" -> "unity" structure using SimpleJson
                var mcpObj = SimpleJson.GetRawObject(json, "mcp");
                if (string.IsNullOrEmpty(mcpObj))
                    return (false, path);

                var unityObj = SimpleJson.GetRawObject(mcpObj, "unity");
                return (!string.IsNullOrEmpty(unityObj), path);
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
            // Build via dictionary + SimpleJson serializer for consistent escaping
            var root = new Dictionary<string, object>
            {
                ["$schema"] = "https://opencode.ai/config.json",
                ["mcp"] = new Dictionary<string, object>
                {
                    ["unity"] = BuildUnityEntryDict(settings)
                }
            };
            return SimpleJson.SerializeObject(root);
        }
        
        // ----------------------------------------------------------------
        // Global OpenCode MCP+ auto-configure
        // ----------------------------------------------------------------
        
        /// <summary>
        /// Merges the Unity MCP server entry into the global OpenCode config (~/.config/opencode/opencode.json).
        /// Preserves all existing settings (plugins, agents, providers, other MCP servers).
        /// Uses SimpleJson for reliable parsing — no brace scanning, no System.Text.Json dependency.
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
        /// Merges the Unity MCP entry into an existing JSON string using SimpleJson parsing.
        /// Handles: file doesn't exist, no mcp section, mcp exists but no unity, unity already exists.
        /// </summary>
        internal static string MergeUnityMcpEntry(string existingJson, McpSettings settings)
        {
            // Parse existing config into a mutable dictionary
            Dictionary<string, object> root;
            try
            {
                var parsed = SimpleJson.ParseElement(existingJson ?? "{}").ToObject();
                root = parsed as Dictionary<string, object>;
                if (root == null)
                    root = new Dictionary<string, object>();
            }
            catch
            {
                Debug.LogWarning("[UnityMCP] Existing config is not valid JSON, starting from scratch");
                root = new Dictionary<string, object>();
            }

            // Ensure "$schema" is present
            if (!root.ContainsKey("$schema"))
                root["$schema"] = "https://opencode.ai/config.json";

            // Build the unity entry
            var unityEntry = BuildUnityEntryDict(settings);

            // Ensure "mcp" object exists
            object mcpObj;
            Dictionary<string, object> mcp;
            if (root.TryGetValue("mcp", out mcpObj) && mcpObj is Dictionary<string, object> existingMcp)
            {
                mcp = existingMcp;
            }
            else
            {
                mcp = new Dictionary<string, object>();
                root["mcp"] = mcp;
            }

            // Set / replace "unity"
            mcp["unity"] = unityEntry;

            // Serialize back with consistent formatting
            return SimpleJson.SerializeObject(root);
        }
        
        /// <summary>
        /// Generates just the Unity MCP server entry JSON object (without the "unity" key wrapper).
        /// </summary>
        public static string GenerateUnityMcpEntry(McpSettings settings)
        {
            return SimpleJson.SerializeObject(BuildUnityEntryDict(settings));
        }

        /// <summary>
        /// Builds the Unity MCP config entry as a Dictionary for serialization.
        /// </summary>
        private static Dictionary<string, object> BuildUnityEntryDict(McpSettings settings)
        {
            var serverPath = McpPathUtility.ToJsonFriendlyPath(settings.ServerBuildOutputPath);
            var projectPath = McpPathUtility.ToJsonFriendlyPath(McpPathUtility.GetProjectRoot());

            var env = new Dictionary<string, object>
            {
                ["UNITY_MCP_WS_HOST"] = settings.Host,
                ["UNITY_MCP_WS_PORT"] = settings.Port.ToString(),
                ["UNITY_MCP_WS_PATH"] = settings.Path,
                ["UNITY_MCP_PROJECT_PATH"] = projectPath
            };

            // Include auth token if configured
            if (!string.IsNullOrEmpty(settings.AuthToken))
                env["UNITY_MCP_AUTH_TOKEN"] = settings.AuthToken;

            var entry = new Dictionary<string, object>
            {
                ["type"] = "local",
                ["command"] = new List<object> { serverPath },
                ["enabled"] = true,
                ["environment"] = env,
                ["timeout"] = 10000
            };

            return entry;
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
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                {
                    error = "Config file is not valid JSON";
                    return false;
                }
                // Validate it actually parses as JSON
                SimpleJson.ParseElement(json);
                return true;
            }
            catch (System.Exception ex)
            {
                error = $"Error reading config: {ex.Message}";
                return false;
            }
        }
    }
}
