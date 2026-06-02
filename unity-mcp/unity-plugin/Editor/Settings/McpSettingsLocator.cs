using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Services;

namespace UnityMCP.Editor.Settings
{
    public static class McpSettingsLocator
    {
        private const string DefaultPath = "Assets/Editor/UnityMCP/McpSettings.asset";
        private const string PackagePath = "Packages/com.unity-mcp.editor/Editor/Settings/McpSettings.asset";
        
        public static McpSettings GetOrCreateSettings()
        {
            var settings = TryLoadSettings();
            if (settings != null) return settings;
            
            // Create new settings
            settings = ScriptableObject.CreateInstance<McpSettings>();
            
            // Set default server paths based on project location
            var projectRoot = McpPathUtility.GetProjectRoot();
            var unityMcpRoot = ResolveDefaultUnityMcpRoot(projectRoot);
            var serverRoot = Path.Combine(unityMcpRoot, "server");
            settings.SetServerRootPath(Path.GetFullPath(serverRoot));
            settings.SetServerBuildOutputPath(Path.GetFullPath(Path.Combine(serverRoot, "bin", "unity-mcp" + (Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : ""))));
            settings.SetOpencodeConfigPath(OpencodeConfigService.GetGlobalConfigPath());
            
            EnsureAssetFolder(Path.GetDirectoryName(DefaultPath)?.Replace('\\', '/'));
            
            AssetDatabase.CreateAsset(settings, DefaultPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[UnityMCP] Created new McpSettings at {DefaultPath}");
            return settings;
        }

        private static string ResolveDefaultUnityMcpRoot(string projectRoot)
        {
            var candidates = new[]
            {
                Path.Combine(projectRoot, "unity-mcp"),
                Path.Combine(projectRoot, "..", "unity-mcp")
            };

            foreach (var candidate in candidates)
            {
                var fullPath = Path.GetFullPath(candidate);
                if (Directory.Exists(Path.Combine(fullPath, "server")))
                    return fullPath;
            }

            return Path.GetFullPath(candidates[0]);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (string.IsNullOrWhiteSpace(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
                return;

            if (!assetFolder.StartsWith("Assets"))
                throw new IOException($"Settings folder must be under Assets: {assetFolder}");

            var projectRoot = McpPathUtility.GetProjectRoot();
            var relative = assetFolder == "Assets" ? string.Empty : assetFolder.Substring("Assets".Length).TrimStart('/');
            var absolute = Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absolute);
            AssetDatabase.Refresh();
        }
        
        public static McpSettings TryLoadSettings()
        {
            // Try default path first
            var settings = AssetDatabase.LoadAssetAtPath<McpSettings>(DefaultPath);
            if (settings != null) return settings;
            
            // Search for any McpSettings asset
            var guids = AssetDatabase.FindAssets("t:McpSettings");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                settings = AssetDatabase.LoadAssetAtPath<McpSettings>(path);
                if (settings != null)
                {
                    Debug.LogWarning($"[UnityMCP] Found settings at non-default path: {path}");
                    return settings;
                }
            }
            
            return null;
        }
    }
}
