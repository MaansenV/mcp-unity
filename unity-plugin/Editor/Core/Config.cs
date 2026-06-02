#if UNITY_EDITOR
using System;
using UnityMCP.Editor.Settings;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Configuration provider that reads from McpSettings ScriptableObject.
    /// Single Source of Truth for all configuration values.
    /// </summary>
    public static class Config
    {
        private static McpSettings s_Settings;
        
        public static McpSettings Settings
        {
            get
            {
                if (s_Settings == null)
                    s_Settings = McpSettingsLocator.GetOrCreateSettings();
                return s_Settings;
            }
        }
        
        // WebSocket
        public static string WebSocketUrl => Settings.WebSocketUrl;
        public static TimeSpan ConnectTimeout => TimeSpan.FromSeconds(Settings.ConnectTimeoutSeconds);
        public static TimeSpan ReceiveTimeout => TimeSpan.FromSeconds(30);
        public static TimeSpan ReconnectDelay => TimeSpan.FromSeconds(Settings.ReconnectDelaySeconds);
        public static TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(Settings.HeartbeatIntervalSeconds);
        public static bool AutoReconnect => Settings.AutoReconnect;
        public static int ReceiveBufferSize => 16 * 1024;
        
        // Server
        public static string ServerRootPath => Settings.ServerRootPath;
        public static string ServerBuildOutputPath => Settings.ServerBuildOutputPath;
        public static bool AutoStartServer => Settings.AutoStartServer;
        public static bool AutoConnect => Settings.AutoConnect;
        
        /// <summary>
        /// Force reload settings from asset.
        /// </summary>
        public static void ReloadSettings()
        {
            s_Settings = null;
        }
    }
}
#endif
