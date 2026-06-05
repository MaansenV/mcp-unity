#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;

namespace McpUnity.Profiler
{
    public sealed class RawFrameDataViewProvider : IProfilerHistoryProvider
    {
        private readonly IProfilerDriverReflection _profilerDriver;
        private readonly IRawFrameDataViewAdapter _rawFrameDataViewAdapter;

        public string ProviderId => "raw_frame_data_view";
        public string DisplayName => "Raw Frame Data View";
        public int Priority => 1000;
        public bool IsAvailable { get; }
        public ProfilerHistoryCapabilities Capabilities { get; }
        public string AvailabilityReason { get; }

        public RawFrameDataViewProvider(IProfilerDriverReflection profilerDriver, IRawFrameDataViewAdapter rawFrameDataViewAdapter)
        {
            _profilerDriver = profilerDriver;
            _rawFrameDataViewAdapter = rawFrameDataViewAdapter;

            IsAvailable = profilerDriver.IsAvailable && rawFrameDataViewAdapter.IsAvailable;

            if (IsAvailable)
            {
                Capabilities = ProfilerHistoryCapabilities.RecordingControl |
                              ProfilerHistoryCapabilities.HistoricalFrames |
                              ProfilerHistoryCapabilities.SelectedFrame |
                              ProfilerHistoryCapabilities.FrameTiming |
                              ProfilerHistoryCapabilities.ThreadHierarchy |
                              ProfilerHistoryCapabilities.SampleHierarchy |
                              ProfilerHistoryCapabilities.SampleMetadata |
                              ProfilerHistoryCapabilities.CpuModule |
                              ProfilerHistoryCapabilities.SpikeAnalysis |
                              ProfilerHistoryCapabilities.BaselineComparison |
                              ProfilerHistoryCapabilities.RequiresReflection |
                              ProfilerHistoryCapabilities.UsesInternalUnityApi;
                AvailabilityReason = "Available";
            }
            else
            {
                Capabilities = ProfilerHistoryCapabilities.None;
                AvailabilityReason = !profilerDriver.IsAvailable ? profilerDriver.AvailabilityReason : rawFrameDataViewAdapter.AvailabilityReason;
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
            error = null;

            if (!TryGetFrameRange(out var range, out error)) return false;
            if (!range.IsValid) { error = "No valid frame range"; return false; }

            var resolver = new FrameRangeResolver();
            if (!resolver.TryResolveQueryRange(query, range, out var resolvedRange, out error)) return false;

            var summaries = new List<ProfilerFrameSummary>();
            var maxFrames = Math.Min(query.MaxFrames, resolvedRange.FrameCount);

            for (int i = 0; i < maxFrames; i++)
            {
                var frameIndex = resolvedRange.LastFrameIndex - i;

                // Use real frame time from ProfilerDriver to avoid double-counting
                // hierarchical sample times. Fall back to sample aggregation only if
                // the driver API is unavailable.
                double frameTimeMs = 0;
                if (!_profilerDriver.TryGetFrameTimeMs(frameIndex, out frameTimeMs, out _))
                {
                    frameTimeMs = SumRootSampleTimes(frameIndex, query);
                }

                string topMarker = null;
                double topMarkerTime = 0;
                string primaryThread = null;

                int threadCount = 0;
                if (!_rawFrameDataViewAdapter.TryGetThreadCount(frameIndex, out threadCount, out error))
                {
                    error = $"Failed to get thread count for frame {frameIndex}: {error}";
                    return false;
                }

                for (int t = 0; t < Math.Min(threadCount, query.MaxThreads); t++)
                {
                    if (!_rawFrameDataViewAdapter.TryOpenFrame(frameIndex, t, out var view, out error))
                        continue;

                    try
                    {
                        if (!_rawFrameDataViewAdapter.TryGetThreadName(view, out var threadName, out var groupName, out _))
                            threadName = $"Thread {t}";

                        if (primaryThread == null) primaryThread = threadName;

                        if (!_rawFrameDataViewAdapter.TryGetSampleCount(view, out var sampleCount, out _))
                            continue;

                        for (int s = 0; s < Math.Min(sampleCount, query.MaxSamplesPerThread); s++)
                        {
                            if (!_rawFrameDataViewAdapter.TryReadSample(view, s, out var sample, out _))
                                continue;

                            if (sample == null) continue;

                            // Top marker tracks largest self-time sample, not nested totals
                            if (sample.SelfTimeMs > topMarkerTime)
                            {
                                topMarkerTime = sample.SelfTimeMs;
                                topMarker = sample.Name;
                            }
                        }
                    }
                    finally
                    {
                        _rawFrameDataViewAdapter.TryDisposeView(view, out _);
                    }
                }

                summaries.Add(new ProfilerFrameSummary
                {
                    FrameIndex = frameIndex,
                    FrameTimeMs = frameTimeMs,
                    CpuTimeMs = frameTimeMs,
                    IsSelected = frameIndex == range.SelectedFrameIndex,
                    IsCurrent = frameIndex == range.CurrentFrameIndex,
                    PrimaryThreadName = primaryThread,
                    TopMarkerName = topMarker,
                    TopMarkerTimeMs = topMarkerTime
                });
            }

            summaries.Reverse();
            frames = summaries;
            return true;
        }

        /// <summary>
        /// Fallback: aggregate main-thread root sample times for a frame.
        /// Used only if ProfilerDriver.GetFrameTimeMs is unavailable.
        /// Picks the first thread and sums root-level samples to avoid double counting.
        /// </summary>
        private double SumRootSampleTimes(int frameIndex, ProfilerFrameQuery query)
        {
            double total = 0;

            if (!_rawFrameDataViewAdapter.TryGetThreadCount(frameIndex, out var threadCount, out _))
                return 0;

            // Main thread is typically index 0; sum only its root samples.
            if (threadCount == 0) return 0;
            if (!_rawFrameDataViewAdapter.TryOpenFrame(frameIndex, 0, out var view, out _))
                return 0;

            try
            {
                if (!_rawFrameDataViewAdapter.TryGetSampleCount(view, out var sampleCount, out _))
                    return 0;

                for (int s = 0; s < Math.Min(sampleCount, query.MaxSamplesPerThread); s++)
                {
                    if (!_rawFrameDataViewAdapter.TryReadSample(view, s, out var sample, out _))
                        continue;
                    if (sample == null) continue;

                    // Root samples have no parent
                    if (sample.ParentSampleId < 0)
                        total += sample.TotalTimeMs;
                }
            }
            finally
            {
                _rawFrameDataViewAdapter.TryDisposeView(view, out _);
            }

            return total;
        }

        public bool TryGetFrameData(ProfilerFrameQuery query, out ProfilerFrameData frameData, out string error)
        {
            frameData = null;
            error = null;

            var resolver = new FrameRangeResolver();
            if (!TryGetFrameRange(out var range, out error)) return false;
            if (!range.IsValid) { error = "No valid frame range"; return false; }

            if (!resolver.TryResolveFrame(query.Frame, range, out var frameIndex, out error)) return false;

            // If threads are not requested, return an empty thread list but still
            // report a valid frame time so callers can see "frame exists, no detail".
            if (!query.IncludeThreads)
            {
                double quickFrameTime = 0;
                _profilerDriver.TryGetFrameTimeMs(frameIndex, out quickFrameTime, out _);

                frameData = new ProfilerFrameData
                {
                    FrameIndex = frameIndex,
                    FrameTimeMs = quickFrameTime,
                    CpuTimeMs = quickFrameTime,
                    IsSelected = frameIndex == range.SelectedFrameIndex,
                    IsCurrent = frameIndex == range.CurrentFrameIndex,
                    ProviderId = ProviderId,
                    CapabilitiesUsed = Capabilities,
                    Threads = Array.Empty<ProfilerThreadData>(),
                    WasTruncated = false
                };
                return true;
            }

            if (!_rawFrameDataViewAdapter.TryGetThreadCount(frameIndex, out var threadCount, out error))
            {
                error = $"Failed to get thread count: {error}";
                return false;
            }

            var threads = new List<ProfilerThreadData>();
            int totalSamples = 0;
            bool truncated = false;
            string truncationReason = null;
            int skippedThreads = 0;
            var warnings = new List<string>();

            for (int t = 0; t < Math.Min(threadCount, query.MaxThreads); t++)
            {
                if (!_rawFrameDataViewAdapter.TryOpenFrame(frameIndex, t, out var view, out error))
                {
                    skippedThreads++;
                    warnings.Add($"Thread {t}: openFrame failed: {error}");
                    continue;
                }

                try
                {
                    if (!_rawFrameDataViewAdapter.TryGetThreadName(view, out var threadName, out var groupName, out _))
                    {
                        threadName = $"Thread {t}";
                        groupName = "Unknown";
                    }

                    if (!_rawFrameDataViewAdapter.TryGetSampleCount(view, out var sampleCount, out _))
                    {
                        skippedThreads++;
                        warnings.Add($"Thread {t} ({threadName}): sampleCount failed");
                        continue;
                    }

                    var samples = new List<ProfilerSampleData>();

                    for (int s = 0; s < Math.Min(sampleCount, query.MaxSamplesPerThread); s++)
                    {
                        if (!_rawFrameDataViewAdapter.TryReadSample(view, s, out var sample, out _))
                            continue;

                        if (sample == null) continue;

                        // Respect MaxDepth: 0 = unlimited, otherwise drop samples deeper than the limit
                        if (query.MaxDepth > 0 && sample.Depth > query.MaxDepth)
                            continue;

                        var sampleName = sample.Name ?? string.Empty;
                        var threadNameSafe = threadName ?? string.Empty;

                        if (query.MarkerFilters != null && query.MarkerFilters.Count > 0)
                        {
                            bool matches = false;
                            foreach (var filter in query.MarkerFilters)
                            {
                                if (sampleName.Contains(filter ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                            if (!matches) continue;
                        }

                        if (!string.IsNullOrEmpty(query.SampleNameFilter) &&
                            !sampleName.Contains(query.SampleNameFilter, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!string.IsNullOrEmpty(query.ThreadNameFilter) &&
                            !threadNameSafe.Contains(query.ThreadNameFilter, StringComparison.OrdinalIgnoreCase))
                            continue;

                        sample.ThreadName = threadName;
                        samples.Add(sample);
                        totalSamples++;

                        if (totalSamples >= (query.Serialization != null ? query.Serialization.MaxSamplesTotal : 5000))
                        {
                            truncated = true;
                            truncationReason = "MaxSamplesTotal limit reached";
                            break;
                        }
                    }

                    if (query.IncludeSampleHierarchy)
                    {
                        samples = BuildHierarchy(samples);
                    }

                    threads.Add(new ProfilerThreadData
                    {
                        ThreadIndex = t,
                        ThreadName = threadName,
                        GroupName = groupName,
                        TotalTimeMs = samples.Where(s => s.ParentSampleId < 0).Sum(s => s.TotalTimeMs),
                        SelfTimeMs = samples.Sum(s => s.SelfTimeMs),
                        Samples = samples,
                        WasTruncated = truncated
                    });
                }
                finally
                {
                    _rawFrameDataViewAdapter.TryDisposeView(view, out _);
                }

                if (truncated) break;
            }

            if (skippedThreads > 0)
            {
                warnings.Add($"Skipped {skippedThreads} thread(s) due to reflection/IO errors");
            }

            // Use real frame time from ProfilerDriver to avoid double-counting
            // hierarchical sample times. Fall back to per-thread root aggregation
            // only if the driver API is unavailable.
            double frameTimeMs = 0;
            if (_profilerDriver.TryGetFrameTimeMs(frameIndex, out frameTimeMs, out _))
            {
                // FrameTime is the wall-clock duration; CpuTime is main-thread work.
                // For now we treat CpuTime = FrameTime as the conservative estimate.
            }
            else
            {
                frameTimeMs = threads.Where(t => t.Samples != null && t.Samples.Count > 0)
                                      .Sum(t => t.Samples.Where(s => s.ParentSampleId < 0)
                                                          .Sum(s => s.TotalTimeMs));
            }

            frameData = new ProfilerFrameData
            {
                FrameIndex = frameIndex,
                FrameTimeMs = frameTimeMs,
                CpuTimeMs = frameTimeMs,
                IsSelected = frameIndex == range.SelectedFrameIndex,
                IsCurrent = frameIndex == range.CurrentFrameIndex,
                ProviderId = ProviderId,
                CapabilitiesUsed = Capabilities,
                Threads = threads,
                WasTruncated = truncated,
                TruncationReason = truncationReason,
                Warnings = warnings.Count > 0 ? warnings : null
            };

            return true;
        }

        private List<ProfilerSampleData> BuildHierarchy(List<ProfilerSampleData> flatSamples)
        {
            if (flatSamples == null || flatSamples.Count == 0)
                return new List<ProfilerSampleData>();

            var sampleMap = new Dictionary<int, ProfilerSampleData>();
            var roots = new List<ProfilerSampleData>();

            // Build the map only with unique sample IDs
            foreach (var sample in flatSamples)
            {
                if (!sampleMap.ContainsKey(sample.SampleId))
                    sampleMap[sample.SampleId] = sample;
            }

            // If no parent info available, treat all as roots
            bool hasAnyParent = flatSamples.Any(s => s.ParentSampleId >= 0);
            if (!hasAnyParent)
            {
                return flatSamples;
            }

            // Use parallel lists for children to avoid casting IReadOnlyList back to List
            var childrenLists = new Dictionary<int, List<ProfilerSampleData>>();

            foreach (var sample in flatSamples)
            {
                if (sample.ParentSampleId >= 0 && sampleMap.TryGetValue(sample.ParentSampleId, out var parent))
                {
                    if (!childrenLists.TryGetValue(parent.SampleId, out var children))
                    {
                        children = new List<ProfilerSampleData>();
                        childrenLists[parent.SampleId] = children;
                    }
                    children.Add(sample);
                }
                else
                {
                    roots.Add(sample);
                }
            }

            // Attach children lists to parents as IReadOnlyList<T>
            foreach (var kvp in childrenLists)
            {
                if (sampleMap.TryGetValue(kvp.Key, out var parent))
                {
                    parent.Children = kvp.Value;
                }
            }

            return roots;
        }

        public bool Supports(ProfilerHistoryCapabilities requiredCapabilities)
        {
            return (Capabilities & requiredCapabilities) == requiredCapabilities;
        }

        public void Dispose() { }
    }
}
#endif