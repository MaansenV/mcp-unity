using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public class ProfilerThreadData
    {
        public int ThreadIndex { get; set; }
        public string ThreadName { get; set; }
        public string GroupName { get; set; }

        public double TotalTimeMs { get; set; }
        public double SelfTimeMs { get; set; }

        public IReadOnlyList<ProfilerSampleData> Samples { get; set; }

        public bool WasTruncated { get; set; }
    }
}