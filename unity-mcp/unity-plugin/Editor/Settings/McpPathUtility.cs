#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor.Settings
{
    /// <summary>
    /// Centralized project-root resolution.
    /// All code that needs the Unity project root should use this instead of Directory.GetCurrentDirectory().
    /// </summary>
    public static class McpPathUtility
    {
        private static string s_ProjectRoot;

        /// <summary>
        /// Absolute path to the Unity project root (the folder that contains Assets/).
        /// Derived from Application.dataPath which is always reliable in the editor.
        /// </summary>
        public static string GetProjectRoot()
        {
            if (string.IsNullOrEmpty(s_ProjectRoot))
                s_ProjectRoot = Path.GetFullPath(
                    Directory.GetParent(Application.dataPath)?.FullName
                    ?? Application.dataPath);
            return s_ProjectRoot;
        }

        /// <summary>
        /// Normalise a path for JSON / config embedding (forward-slashes, no trailing slash).
        /// </summary>
        public static string ToJsonFriendlyPath(string absolutePath)
        {
            return absolutePath?.Replace("\\", "/").TrimEnd('/');
        }
    }
}
#endif
