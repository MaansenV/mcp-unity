using System;
using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public class ProfilerFrameQuery
    {
        public FrameReference Frame { get; set; } = FrameReference.Latest();

        public FrameReference StartFrame { get; set; } = FrameReference.RelativeToLatest(-119);
        public FrameReference EndFrame { get; set; } = FrameReference.Latest();

        public int MaxFrames { get; set; } = 60;
        public int MaxThreads { get; set; } = 8;
        public int MaxSamplesPerThread { get; set; } = 256;
        public int MaxDepth { get; set; } = 8;

        public bool IncludeSampleHierarchy { get; set; } = true;
        public bool IncludeSampleMetadata { get; set; } = false;
        public bool IncludeThreads { get; set; } = true;
        public bool IncludeCounters { get; set; } = false;

        public string ThreadNameFilter { get; set; }
        public string SampleNameFilter { get; set; }

        public IReadOnlyList<string> MarkerFilters { get; set; }

        public double SpikeThresholdMs { get; set; } = 33.333;
        public double SpikeThresholdMultiplier { get; set; } = 2.0;

        public string SortBy { get; set; } = "frame_index";
        public string SortDirection { get; set; } = "asc";

        public ProfilerSerializationOptions Serialization { get; set; }

        /// <summary>
        /// Clamp and normalize all values to safe runtime ranges. Call this at
        /// service/tool boundaries so downstream code never sees negative or
        /// out-of-bounds limits.
        /// </summary>
        public ProfilerFrameQuery Normalize()
        {
            MaxFrames = Math.Clamp(MaxFrames, 1, 300);
            MaxThreads = Math.Clamp(MaxThreads, 1, 64);
            MaxSamplesPerThread = Math.Clamp(MaxSamplesPerThread, 1, 5000);
            MaxDepth = Math.Clamp(MaxDepth, 0, 64);

            if (SpikeThresholdMs < 0) SpikeThresholdMs = 0;
            if (SpikeThresholdMultiplier <= 0) SpikeThresholdMultiplier = 1.0;

            Serialization ??= new ProfilerSerializationOptions();

            if (string.IsNullOrWhiteSpace(SortBy)) SortBy = "frame_index";
            if (string.IsNullOrWhiteSpace(SortDirection)) SortDirection = "asc";

            return this;
        }
    }
}