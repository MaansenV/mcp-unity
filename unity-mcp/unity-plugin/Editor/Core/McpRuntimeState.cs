#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.Core
{
    /// <summary>
    /// Runtime state for UI display. Tracks connection, clients, and setup progress.
    /// </summary>
    [Serializable]
    public class McpRuntimeState
    {
        public UnityMCP.Editor.ConnectionState ConnectionState = UnityMCP.Editor.ConnectionState.Disconnected;
        public bool IsServerRunning;
        public int? ServerProcessId;
        public DateTime? LastConnectedAt;
        public DateTime? LastDisconnectedAt;
        public string LastError;
        public int DiscoveredToolCount;
        public bool IsSetupRunning;
        public float SetupProgress;
        public string CurrentOperation;
        
        [NonSerialized] private List<ConnectedClientInfo> _connectedClients = new();
        public IReadOnlyList<ConnectedClientInfo> ConnectedClients => _connectedClients;
        public int ClientCount => _connectedClients.Count;
        
        public event Action Changed;
        
        public void NotifyChanged() => Changed?.Invoke();
        
        public void UpdateFromPlugin(UnityMCP.Editor.ConnectionState state, bool serverRunning, int? processId)
        {
            var prev = ConnectionState;
            ConnectionState = state;
            IsServerRunning = serverRunning;
            ServerProcessId = processId;
            
            if (state == UnityMCP.Editor.ConnectionState.Connected && prev != UnityMCP.Editor.ConnectionState.Connected)
                LastConnectedAt = DateTime.Now;
            else if (state == UnityMCP.Editor.ConnectionState.Disconnected && prev != UnityMCP.Editor.ConnectionState.Disconnected)
                LastDisconnectedAt = DateTime.Now;
            
            NotifyChanged();
        }
        
        public void SetError(string error)
        {
            LastError = error;
            ConnectionState = UnityMCP.Editor.ConnectionState.Error;
            NotifyChanged();
        }
        
        public void SetSetupProgress(float progress, string operation)
        {
            SetupProgress = progress;
            CurrentOperation = operation;
            IsSetupRunning = progress < 1f;
            NotifyChanged();
        }

        // --- Client Management ---

        public void AddClient(ConnectedClientInfo client)
        {
            if (client == null) return;
            
            // Replace existing client with same ID
            _connectedClients.RemoveAll(c => c.Id == client.Id);
            _connectedClients.Add(client);
            NotifyChanged();
        }

        public void RemoveClient(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) return;
            
            var removed = _connectedClients.RemoveAll(c => c.Id == clientId);
            if (removed > 0)
                NotifyChanged();
        }

        public void UpdateClientLastSeen(string clientId)
        {
            var client = _connectedClients.FirstOrDefault(c => c.Id == clientId);
            if (client != null)
            {
                client.LastSeen = DateTime.Now;
                NotifyChanged();
            }
        }

        public void ClearClients()
        {
            if (_connectedClients.Count == 0) return;
            _connectedClients.Clear();
            NotifyChanged();
        }

        public ConnectedClientInfo GetClient(string clientId)
        {
            return _connectedClients.FirstOrDefault(c => c.Id == clientId);
        }

        public string FormatClientSummary()
        {
            if (_connectedClients.Count == 0)
                return "No clients connected";
            
            if (_connectedClients.Count == 1)
                return $"1 client: {_connectedClients[0].Name}";
            
            return $"{_connectedClients.Count} clients connected";
        }
    }
}
#endif
