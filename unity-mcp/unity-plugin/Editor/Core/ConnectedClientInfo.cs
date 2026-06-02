#if UNITY_EDITOR
using System;

namespace UnityMCP.Editor.Core
{
    /// <summary>
    /// Information about a connected CLI client (e.g. opencode, claude, custom).
    /// </summary>
    [Serializable]
    public class ConnectedClientInfo
    {
        public string Id;
        public string Name;
        public string RemoteAddress;
        public DateTime ConnectedAt;
        public DateTime LastSeen;

        public ConnectedClientInfo() { }

        public ConnectedClientInfo(string id, string name, string remoteAddress)
        {
            Id = id;
            Name = name;
            RemoteAddress = remoteAddress;
            ConnectedAt = DateTime.Now;
            LastSeen = DateTime.Now;
        }

        public string FormatUptime()
        {
            var elapsed = DateTime.Now - ConnectedAt;
            if (elapsed.TotalHours >= 1)
                return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
            if (elapsed.TotalMinutes >= 1)
                return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
            return $"{(int)elapsed.TotalSeconds}s";
        }

        public string FormatLastSeen()
        {
            var elapsed = DateTime.Now - LastSeen;
            if (elapsed.TotalSeconds < 10)
                return "just now";
            if (elapsed.TotalMinutes < 1)
                return $"{(int)elapsed.TotalSeconds}s ago";
            if (elapsed.TotalHours < 1)
                return $"{(int)elapsed.TotalMinutes}m ago";
            return $"{(int)elapsed.TotalHours}h ago";
        }
    }
}
#endif
