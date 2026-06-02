#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tools
{
    /// <summary>
    /// Centralized path validation for Unity MCP asset operations.
    /// Prevents path traversal attacks and ensures all paths stay within the Assets folder.
    /// </summary>
    public static class AssetPathValidator
    {
        private static string s_ProjectRoot;
        private static string s_AssetsRoot;

        private static string ProjectRoot
        {
            get
            {
                if (string.IsNullOrEmpty(s_ProjectRoot))
                    s_ProjectRoot = Path.GetFullPath(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath);
                return s_ProjectRoot;
            }
        }

        private static string AssetsRoot
        {
            get
            {
                if (string.IsNullOrEmpty(s_AssetsRoot))
                    s_AssetsRoot = Path.GetFullPath(Path.Combine(ProjectRoot, "Assets"));
                return s_AssetsRoot;
            }
        }

        /// <summary>
        /// Validate and normalize an asset path. Ensures it stays within the Assets folder.
        /// </summary>
        /// <param name="input">The input path from user/MCP</param>
        /// <param name="normalizedAssetPath">The validated and normalized asset path (relative, e.g. "Assets/Foo/Bar")</param>
        /// <param name="error">Error message if validation fails</param>
        /// <param name="mustStartWithAssets">If true, path must start with "Assets"</param>
        /// <returns>True if path is valid</returns>
        public static bool TryValidateAssetPath(string input, out string normalizedAssetPath, out string error, bool mustStartWithAssets = true)
        {
            normalizedAssetPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Path cannot be empty";
                return false;
            }

            // Normalize separators
            var normalized = input.Replace('\\', '/').Trim();

            // Reject absolute paths for asset operations
            if (Path.IsPathRooted(normalized))
            {
                error = $"Absolute paths are not allowed for asset operations: {input}";
                return false;
            }

            // Reject URI-style paths
            if (normalized.Contains("://") || normalized.Contains("://"))
            {
                error = $"URI-style paths are not allowed: {input}";
                return false;
            }

            // Get full path relative to project root
            var fullPath = Path.GetFullPath(Path.Combine(ProjectRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));

            // Check if path escapes Assets folder
            if (!fullPath.StartsWith(AssetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullPath, AssetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Path must stay inside Assets folder. Got: {input}";
                return false;
            }

            // Convert back to Unity asset path
            var assetPath = fullPath.Substring(ProjectRoot.Length).Replace(Path.DirectorySeparatorChar, '/').TrimStart('/');

            // Validate starts with Assets if required
            if (mustStartWithAssets && !assetPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Path must start with 'Assets'. Got: {input}";
                return false;
            }

            // Reject path traversal attempts
            if (normalized.Contains(".."))
            {
                error = $"Path traversal (..) is not allowed: {input}";
                return false;
            }

            normalizedAssetPath = assetPath;
            return true;
        }

        /// <summary>
        /// Validate an external import source path (absolute paths allowed).
        /// </summary>
        /// <param name="input">The input path</param>
        /// <param name="normalizedAbsolutePath">The validated absolute path</param>
        /// <param name="error">Error message if validation fails</param>
        /// <returns>True if path is valid</returns>
        public static bool TryValidateExternalImportSource(string input, out string normalizedAbsolutePath, out string error)
        {
            normalizedAbsolutePath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Import source path cannot be empty";
                return false;
            }

            // Normalize
            var normalized = input.Replace('/', Path.DirectorySeparatorChar).Trim();

            // Get full path
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(normalized);
            }
            catch (Exception ex)
            {
                error = $"Invalid path: {ex.Message}";
                return false;
            }

            // Check file exists
            if (!File.Exists(fullPath))
            {
                error = $"File does not exist: {fullPath}";
                return false;
            }

            // Reject wildcard characters
            if (fullPath.Contains("*") || fullPath.Contains("?"))
            {
                error = $"Wildcard characters are not allowed: {input}";
                return false;
            }

            normalizedAbsolutePath = fullPath;
            return true;
        }

        /// <summary>
        /// Check if a path is inside the Assets folder (quick check without full validation).
        /// </summary>
        public static bool IsInsideAssets(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            return assetPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolve a path or GUID to a valid asset path.
        /// </summary>
        public static string ResolveAssetPath(string pathOrGuid)
        {
            if (string.IsNullOrWhiteSpace(pathOrGuid))
                return string.Empty;

            // Check if it's already a valid asset path
            if (AssetDatabase.IsValidFolder(pathOrGuid) || AssetDatabase.LoadMainAssetAtPath(pathOrGuid) != null)
                return pathOrGuid;

            // Try as GUID
            var guidPath = AssetDatabase.GUIDToAssetPath(pathOrGuid);
            if (!string.IsNullOrWhiteSpace(guidPath))
                return guidPath;

            return pathOrGuid;
        }
    }
}
#endif
