using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public class ProfilerFrameData
    {
        public int FrameIndex { get; set; }
        public double FrameTimeMs { get; set; }
        public double CpuTimeMs { get; set; }
        public double GpuTimeMs { get; set; }
        public double EditorLoopTimeMs { get; set; }

        public bool IsSelected { get; set; }
        public bool IsCurrent { get; set; }

        public string ProviderId { get; set; }
        public ProfilerHistoryCapabilities CapabilitiesUsed { get; set; }

        public IReadOnlyList<ProfilerThreadData> Threads { get; set; }
        public IReadOnlyDictionary<string, double> Counters { get; set; }

        public bool WasTruncated { get; set; }
        public string TruncationReason { get; set; }

        public IReadOnlyList<string> Warnings { get; set; }
    }
}