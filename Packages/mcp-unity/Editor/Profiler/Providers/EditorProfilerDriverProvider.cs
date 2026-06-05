using System;
using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public sealed class EditorProfilerDriverProvider : IProfilerHistoryProvider
    {
        private readonly IProfilerDriverReflection _profilerDriver;

        public string ProviderId => "editor_profiler_driver";
        public string DisplayName => "Editor Profiler Driver";
        public int Priority => 600;
        public bool IsAvailable { get; }
        public ProfilerHistoryCapabilities Capabilities { get; }
        public string AvailabilityReason { get; }

        public EditorProfilerDriverProvider(IProfilerDriverReflection profilerDriver)
        {
            _profilerDriver = profilerDriver;
            IsAvailable = profilerDriver.IsAvailable;

            if (IsAvailable)
            {
                // Only advertise capabilities that are actually implemented.
                // TryListFrames and TryGetFrameData are not implemented.
                var caps = ProfilerHistoryCapabilities.RecordingControl |
                          ProfilerHistoryCapabilities.FrameTiming |
                          ProfilerHistoryCapabilities.RequiresReflection |
                          ProfilerHistoryCapabilities.UsesInternalUnityApi;

                if (profilerDriver.Discovery.HasMember("firstFrameIndex") && profilerDriver.Discovery.HasMember("lastFrameIndex"))
                    caps |= ProfilerHistoryCapabilities.HistoricalFrames;

                if (profilerDriver.Discovery.HasMember("selectedFrameIndex"))
                    caps |= ProfilerHistoryCapabilities.SelectedFrame;

                Capabilities = caps;
                AvailabilityReason = "Available via reflection (status/recording/range only - frame listing not implemented)";
            }
            else
            {
                Capabilities = ProfilerHistoryCapabilities.None;
                AvailabilityReason = profilerDriver.AvailabilityReason;
            }
        }

        public ProfilerProviderDescriptor GetDescriptor()
        {
            return new ProfilerProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = DisplayName,
                Priority = Priority,
                IsAvailable = IsAvailable,
                AvailabilityReason = AvailabilityReason,
                Capabilities = Capabilities,
                UnityVersion = UnityEngine.Application.unityVersion,
                UsesReflection = true
            };
        }

        public ProfilerHistoryStatus GetStatus()
        {
            var status = new ProfilerHistoryStatus
            {
                ProfilerSupported = IsAvailable,
                ActiveProviderId = ProviderId,
                ActiveProviderName = DisplayName,
                Capabilities = Capabilities,
                Providers = new List<ProfilerProviderDescriptor> { GetDescriptor() },
                UnityVersion = UnityEngine.Application.unityVersion
            };

            if (IsAvailable)
            {
                _profilerDriver.TryGetRecordingEnabled(out var recording, out _);
                status.RecordingEnabled = recording;

                _profilerDriver.TryGetFirstFrameIndex(out var first, out _);
                _profilerDriver.TryGetLastFrameIndex(out var last, out _);
                _profilerDriver.TryGetCurrentFrameIndex(out var current, out _);
                _profilerDriver.TryGetSelectedFrameIndex(out var selected, out _);

                status.FrameRange = new ProfilerFrameRange
                {
                    FirstFrameIndex = first,
                    LastFrameIndex = last,
                    CurrentFrameIndex = current,
                    SelectedFrameIndex = selected,
                    FrameCount = Math.Max(0, last - first + 1),
                    HasSelection = selected >= first && selected <= last,
                    IsValid = first <= last
                };
                status.HistoryAvailable = status.FrameRange.IsValid && status.FrameRange.FrameCount > 0;
            }

            return status;
        }

        public bool IsRecordingEnabled()
        {
            if (!_profilerDriver.TryGetRecordingEnabled(out var enabled, out _)) return false;
            return enabled;
        }

        public bool SetRecordingEnabled(bool enabled, out string message)
        {
            return _profilerDriver.TrySetRecordingEnabled(enabled, out message);
        }

        public bool TryGetSelectedFrame(out int frameIndex, out string error)
        {
            if (!_profilerDriver.IsAvailable) { frameIndex = -1; error = "ProfilerDriver not available"; return false; }
            return _profilerDriver.TryGetSelectedFrameIndex(out frameIndex, out error);
        }

        public bool TryGetFrameRange(out ProfilerFrameRange range, out string error)
        {
            range = new ProfilerFrameRange();
            error = null;

            if (!_profilerDriver.TryGetFirstFrameIndex(out var first, out error)) return false;
            if (!_profilerDriver.TryGetLastFrameIndex(out var last, out error)) return false;
            if (!_profilerDriver.TryGetCurrentFrameIndex(out var current, out error)) return false;
            _profilerDriver.TryGetSelectedFrameIndex(out var selected, out _);

            range.FirstFrameIndex = first;
            range.LastFrameIndex = last;
            range.CurrentFrameIndex = current;
            range.SelectedFrameIndex = selected;
            range.FrameCount = Math.Max(0, last - first + 1);
            range.HasSelection = selected >= first && selected <= last;
            range.IsValid = first <= last;

            return true;
        }

        public bool TryListFrames(ProfilerFrameQuery query, out IReadOnlyList<ProfilerFrameSummary> frames, out string error)
        {
            frames = Array.Empty<ProfilerFrameSummary>();
            error = "Frame listing not supported by EditorProfilerDriverProvider";
            return false;
        }

        public bool TryGetFrameData(ProfilerFrameQuery query, out ProfilerFrameData frameData, out string error)
        {
            frameData = null;
            error = "Detailed frame data not supported by EditorProfilerDriverProvider";
            return false;
        }

        public bool Supports(ProfilerHistoryCapabilities requiredCapabilities)
        {
            return (Capabilities & requiredCapabilities) == requiredCapabilities;
        }

        public void Dispose() { }
    }
}