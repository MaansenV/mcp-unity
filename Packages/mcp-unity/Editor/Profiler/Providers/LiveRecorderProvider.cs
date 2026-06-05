using System;
using System.Collections.Generic;
using UnityEngine;
using UnityProfiler = UnityEngine.Profiling.Profiler;

namespace McpUnity.Profiler
{
    public sealed class LiveRecorderProvider : IProfilerHistoryProvider
    {
        public string ProviderId => "live_recorder";
        public string DisplayName => "Live Current Frame Recorder";
        public int Priority => 100;
        public bool IsAvailable => true;
        public ProfilerHistoryCapabilities Capabilities { get; }
        public string AvailabilityReason => "Public Unity runtime profiler APIs available.";

        public LiveRecorderProvider()
        {
            Capabilities = ProfilerHistoryCapabilities.RecordingControl |
                          ProfilerHistoryCapabilities.CurrentFrameOnly |
                          ProfilerHistoryCapabilities.FrameTiming;
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
                UnityVersion = Application.unityVersion,
                UsesReflection = false
            };
        }

        public ProfilerHistoryStatus GetStatus()
        {
            var frameCount = Time.frameCount;
            return new ProfilerHistoryStatus
            {
                ProfilerSupported = true,
                RecordingEnabled = UnityProfiler.enabled,
                HistoryAvailable = false,
                ActiveProviderId = ProviderId,
                ActiveProviderName = DisplayName,
                Capabilities = Capabilities,
                FrameRange = new ProfilerFrameRange
                {
                    FirstFrameIndex = frameCount,
                    LastFrameIndex = frameCount,
                    CurrentFrameIndex = frameCount,
                    FrameCount = 1,
                    HasSelection = false,
                    IsValid = true
                },
                Providers = new List<ProfilerProviderDescriptor> { GetDescriptor() },
                UnityVersion = Application.unityVersion
            };
        }

        public bool IsRecordingEnabled()
        {
            return UnityProfiler.enabled;
        }

        public bool SetRecordingEnabled(bool enabled, out string message)
        {
            try
            {
                UnityProfiler.enabled = enabled;
                message = $"Profiler recording {(enabled ? "enabled" : "disabled")}";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public bool TryGetSelectedFrame(out int frameIndex, out string error)
        {
            frameIndex = -1;
            error = "No profiler selection available in live recorder mode";
            return false;
        }

        public bool TryGetFrameRange(out ProfilerFrameRange range, out string error)
        {
            var frameCount = Time.frameCount;
            range = new ProfilerFrameRange
            {
                FirstFrameIndex = frameCount,
                LastFrameIndex = frameCount,
                CurrentFrameIndex = frameCount,
                FrameCount = 1,
                HasSelection = false,
                IsValid = true
            };
            error = null;
            return true;
        }

        public bool TryListFrames(ProfilerFrameQuery query, out IReadOnlyList<ProfilerFrameSummary> frames, out string error)
        {
            var frameCount = Time.frameCount;
            var summary = new ProfilerFrameSummary
            {
                FrameIndex = frameCount,
                FrameTimeMs = Time.deltaTime * 1000.0,
                CpuTimeMs = Time.deltaTime * 1000.0,
                IsCurrent = true,
                PrimaryThreadName = "Main Thread",
                TopMarkerName = "PlayerLoop"
            };
            frames = new List<ProfilerFrameSummary> { summary };
            error = null;
            return true;
        }

        public bool TryGetFrameData(ProfilerFrameQuery query, out ProfilerFrameData frameData, out string error)
        {
            var frameCount = Time.frameCount;
            frameData = new ProfilerFrameData
            {
                FrameIndex = frameCount,
                FrameTimeMs = Time.deltaTime * 1000.0,
                CpuTimeMs = Time.deltaTime * 1000.0,
                IsCurrent = true,
                ProviderId = ProviderId,
                CapabilitiesUsed = Capabilities,
                Threads = Array.Empty<ProfilerThreadData>(),
                Counters = new Dictionary<string, double>
                {
                    ["totalAllocatedMemory"] = UnityProfiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0),
                    ["totalReservedMemory"] = UnityProfiler.GetTotalReservedMemoryLong() / (1024.0 * 1024.0),
                    ["totalUnusedReservedMemory"] = UnityProfiler.GetTotalUnusedReservedMemoryLong() / (1024.0 * 1024.0),
                    ["monoUsedSize"] = UnityProfiler.GetMonoUsedSizeLong() / (1024.0 * 1024.0),
                    ["monoHeapSize"] = UnityProfiler.GetMonoHeapSizeLong() / (1024.0 * 1024.0)
                }
            };
            error = null;
            return true;
        }

        public bool Supports(ProfilerHistoryCapabilities requiredCapabilities)
        {
            return (Capabilities & requiredCapabilities) == requiredCapabilities;
        }

        public void Dispose() { }
    }
}