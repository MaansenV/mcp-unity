using System;
using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public class ProfilerSampleData
    {
        public int SampleId { get; set; }
        public int ParentSampleId { get; set; }
        public int Depth { get; set; }

        public string Name { get; set; }
        public string Category { get; set; }
        public string ThreadName { get; set; }

        public double TotalTimeMs { get; set; }
        public double SelfTimeMs { get; set; }
        public long AllocatedBytes { get; set; }
        public int CallCount { get; set; }

        public IReadOnlyDictionary<string, string> Metadata { get; set; }
        public IReadOnlyList<ProfilerSampleData> Children { get; set; }

        public bool MatchesFilter(string sampleNameFilter, IReadOnlyList<string> markerFilters)
        {
            var name = Name ?? string.Empty;

            if (!string.IsNullOrEmpty(sampleNameFilter) && !name.Contains(sampleNameFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (markerFilters != null && markerFilters.Count > 0)
            {
                bool matches = false;
                foreach (var filter in markerFilters)
                {
                    if (name.Contains(filter ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                        break;
                    }
                }
                if (!matches) return false;
            }

            return true;
        }
    }
}