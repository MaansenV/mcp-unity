#if UNITY_EDITOR
using System;
using UnityEngine;

namespace UnityMCP.Editor.Settings
{
    public enum McpThemeMode { System, Dark, Light }
    
    [CreateAssetMenu(fileName = "McpSettings", menuName = "Unity MCP/Settings")]
    public class McpSettings : ScriptableObject
    {
        [Header("WebSocket")]
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 8081;
        [SerializeField] private string path = "/ws";
        [SerializeField] private string authToken = "";
        
        [Header("Server")]
        [SerializeField] private string serverRootPath = "";
        [SerializeField] private string serverBuildOutputPath = "";
        [SerializeField] private string opencodeConfigPath = "";
        
        [Header("Auto")]
        [SerializeField] private bool autoStartServer = true;
        [SerializeField] private bool autoConnect = true;
        [SerializeField] private bool autoReconnect = true;
        [SerializeField] private bool autoBuildIfMissing = true;
        
        [Header("Timeouts")]
        [SerializeField] private float connectTimeoutSeconds = 10f;
        [SerializeField] private float reconnectDelaySeconds = 2f;
        [SerializeField] private float heartbeatIntervalSeconds = 15f;
        
        [Header("UI")]
        [SerializeField] private int maxLogEntries = 500;
        [SerializeField] private McpThemeMode theme = McpThemeMode.System;
        
        // Properties
        public string Host => host;
        public int Port => port;
        public string Path => path;
        public string AuthToken => authToken;
        public string WebSocketUrl => $"ws://{host}:{port}{path}";
        public Uri HealthUri => new Uri($"http://{host}:{port}/healthz");
        
        public string ServerRootPath => serverRootPath;
        public string ServerBuildOutputPath => serverBuildOutputPath;
        public string OpencodeConfigPath => opencodeConfigPath;
        
        public bool AutoStartServer => autoStartServer;
        public bool AutoConnect => autoConnect;
        public bool AutoReconnect => autoReconnect;
        public bool AutoBuildIfMissing => autoBuildIfMissing;
        
        public float ConnectTimeoutSeconds => connectTimeoutSeconds;
        public float ReconnectDelaySeconds => reconnectDelaySeconds;
        public float HeartbeatIntervalSeconds => heartbeatIntervalSeconds;
        
        public int MaxLogEntries => maxLogEntries;
        public McpThemeMode Theme => theme;
        
        // Setters for Editor Window
        public void SetHost(string value) => host = value;
        public void SetPort(int value) => port = Mathf.Clamp(value, 1, 65535);
        public void SetPath(string value) => path = value;
        public void SetAuthToken(string value) => authToken = value;
        public void SetServerRootPath(string value) => serverRootPath = value;
        public void SetServerBuildOutputPath(string value) => serverBuildOutputPath = value;
        public void SetOpencodeConfigPath(string value) => opencodeConfigPath = value;
        public void SetAutoStartServer(bool value) => autoStartServer = value;
        public void SetAutoConnect(bool value) => autoConnect = value;
        public void SetAutoReconnect(bool value) => autoReconnect = value;
        public void SetAutoBuildIfMissing(bool value) => autoBuildIfMissing = value;
        public void SetConnectTimeoutSeconds(float value) => connectTimeoutSeconds = Mathf.Max(1f, value);
        public void SetReconnectDelaySeconds(float value) => reconnectDelaySeconds = Mathf.Max(0.1f, value);
        public void SetHeartbeatIntervalSeconds(float value) => heartbeatIntervalSeconds = Mathf.Max(1f, value);
        public void SetMaxLogEntries(int value) => maxLogEntries = Mathf.Clamp(value, 50, 5000);
        public void SetTheme(McpThemeMode value) => theme = value;
        
        /// <summary>
        /// Generate a new auth token if not set.
        /// </summary>
        public void EnsureAuthToken()
        {
            if (string.IsNullOrEmpty(authToken))
            {
                authToken = Guid.NewGuid().ToString("N");
                Save();
            }
        }
        
        public void Save()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
    }
}
#endif
