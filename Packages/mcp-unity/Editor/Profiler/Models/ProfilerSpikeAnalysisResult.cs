using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public class ProfilerSpikeAnalysisResult
    {
        public ProfilerFrameRange AnalyzedRange { get; set; }
        public ProfilerBaselineInfo Baseline { get; set; }

        public IReadOnlyList<ProfilerSpikeInfo> Spikes { get; set; }

        public int FramesAnalyzed { get; set; }
        public int SpikeCount { get; set; }

        public double WorstFrameTimeMs { get; set; }
        public int WorstFrameIndex { get; set; }

        public bool WasTruncated { get; set; }
        public string Message { get; set; }
    }

    public class ProfilerSpikeInfo
    {
        public int FrameIndex { get; set; }
        public double FrameTimeMs { get; set; }
        public double BaselineFrameTimeMs { get; set; }
        public double OverBaselineMs { get; set; }
        public double Multiplier { get; set; }

        public string SuspectedMarkerName { get; set; }
        public double SuspectedMarkerTimeMs { get; set; }
        public string ThreadName { get; set; }

        public ProfilerFrameSummary FrameSummary { get; set; }
    }

    public class ProfilerBaselineInfo
    {
        public double AverageFrameTimeMs { get; set; }
        public double MedianFrameTimeMs { get; set; }
        public double MinFrameTimeMs { get; set; }
        public double MaxFrameTimeMs { get; set; }
        public double StandardDeviationMs { get; set; }
        public int SampleCount { get; set; }
    }
}