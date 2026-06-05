namespace McpUnity.Profiler
{
    public interface IProfilerDriverReflection
    {
        bool IsAvailable { get; }
        string AvailabilityReason { get; }

        ReflectionDiscoveryResult Discovery { get; }

        bool TryGetFirstFrameIndex(out int frameIndex, out string error);
        bool TryGetLastFrameIndex(out int frameIndex, out string error);
        bool TryGetCurrentFrameIndex(out int frameIndex, out string error);
        bool TryGetSelectedFrameIndex(out int frameIndex, out string error);

        bool TryGetRecordingEnabled(out bool enabled, out string error);
        bool TrySetRecordingEnabled(bool enabled, out string error);

        bool TryGetFrameTimeMs(int frameIndex, out double frameTimeMs, out string error);

        bool TryInvokeMethod<T>(
            string logicalMethodName,
            object[] args,
            out T result,
            out string error);
    }
}