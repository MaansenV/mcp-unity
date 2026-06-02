using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityMCP.Editor.Logging
{
    [Serializable]
    public class McpLogBuffer
    {
        private readonly List<McpLogEntry> _entries = new();
        private readonly object _lock = new();
        private int _maxEntries = 500;
        private IReadOnlyList<McpLogEntry> _cachedSnapshot;
        
        public event Action<McpLogEntry> EntryAdded;
        public event Action Cleared;
        
        public IReadOnlyList<McpLogEntry> Entries
        {
            get
            {
                lock (_lock)
                {
                    if (_cachedSnapshot != null)
                        return _cachedSnapshot;

                    _cachedSnapshot = _entries.ToList().AsReadOnly();
                    return _cachedSnapshot;
                }
            }
        }
        
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _entries.Count;
                }
            }
        }
        
        public McpLogBuffer(int maxEntries = 500)
        {
            _maxEntries = Math.Max(50, Math.Min(5000, maxEntries));
        }
        
        public void SetMaxEntries(int maxEntries)
        {
            _maxEntries = Math.Max(50, Math.Min(5000, maxEntries));
            TrimIfNeeded();
        }
        
        public void Add(McpLogLevel level, McpLogCategory category, string message, string details = null)
        {
            var entry = new McpLogEntry(level, category, message, details);
            
            lock (_lock)
            {
                _entries.Add(entry);
                TrimIfNeeded();
                _cachedSnapshot = null;
            }
            
            EntryAdded?.Invoke(entry);
            
            // Also log to Unity console
            switch (level)
            {
                case McpLogLevel.Error:
                    Debug.LogError($"[UnityMCP] {entry.FormattedMessage}");
                    break;
                case McpLogLevel.Warning:
                    Debug.LogWarning($"[UnityMCP] {entry.FormattedMessage}");
                    break;
                default:
                    Debug.Log($"[UnityMCP] {entry.FormattedMessage}");
                    break;
            }
        }
        
        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                _cachedSnapshot = null;
            }
            Cleared?.Invoke();
        }
        
        public List<McpLogEntry> GetFiltered(McpLogCategory? category = null, McpLogLevel? minLevel = null, string search = null)
        {
            lock (_lock)
            {
                var query = _entries.AsEnumerable();
                
                if (category.HasValue)
                    query = query.Where(e => e.Category == category.Value);
                
                if (minLevel.HasValue)
                    query = query.Where(e => e.Level >= minLevel.Value);
                
                if (!string.IsNullOrEmpty(search))
                    query = query.Where(e => e.Message.Contains(search, StringComparison.OrdinalIgnoreCase));
                
                return query.ToList();
            }
        }
        
        public string ExportAll()
        {
            lock (_lock)
            {
                return string.Join("\n", _entries.Select(e => e.FormattedMessage));
            }
        }
        
        private void TrimIfNeeded()
        {
            while (_entries.Count > _maxEntries)
            {
                _entries.RemoveAt(0);
            }

            _cachedSnapshot = null;
        }
    }
}
