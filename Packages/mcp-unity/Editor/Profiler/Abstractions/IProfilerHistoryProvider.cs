using System;
using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public interface IProfilerHistoryProvider : IDisposable
    {
        string ProviderId { get; }
        string DisplayName { get; }
        int Priority { get; }
        bool IsAvailable { get; }
        ProfilerHistoryCapabilities Capabilities { get; }
        string AvailabilityReason { get; }

        ProfilerProviderDescriptor GetDescriptor();

        ProfilerHistoryStatus GetStatus();

        bool IsRecordingEnabled();
        bool SetRecordingEnabled(bool enabled, out string message);

        bool TryGetSelectedFrame(out int frameIndex, out string error);

        bool TryGetFrameRange(out ProfilerFrameRange range, out string error);

        bool TryListFrames(
            ProfilerFrameQuery query,
            out IReadOnlyList<ProfilerFrameSummary> frames,
            out string error);

        bool TryGetFrameData(
            ProfilerFrameQuery query,
            out ProfilerFrameData frameData,
            out string error);

        bool Supports(ProfilerHistoryCapabilities requiredCapabilities);
    }
}