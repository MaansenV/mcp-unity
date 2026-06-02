#if UNITY_EDITOR
namespace UnityMCP.Editor
{
    public enum ConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Compiling = 3,
        Error = 4
    }
}
#endif
