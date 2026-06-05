namespace McpUnity.Profiler
{
    public class ProfilerFrameSummary
    {
        public int FrameIndex { get; set; }
        public double FrameTimeMs { get; set; }
        public double CpuTimeMs { get; set; }
        public double GpuTimeMs { get; set; }

        public bool IsSelected { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsSpike { get; set; }

        public string PrimaryThreadName { get; set; }
        public string TopMarkerName { get; set; }
        public double TopMarkerTimeMs { get; set; }
    }
}