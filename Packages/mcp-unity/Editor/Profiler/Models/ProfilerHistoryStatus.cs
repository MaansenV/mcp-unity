using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public class ProfilerHistoryStatus
    {
        public bool ProfilerSupported { get; set; }
        public bool RecordingEnabled { get; set; }
        public bool HistoryAvailable { get; set; }

        public string ActiveProviderId { get; set; }
        public string ActiveProviderName { get; set; }
        public ProfilerHistoryCapabilities Capabilities { get; set; }

        public ProfilerFrameRange FrameRange { get; set; }

        public IReadOnlyList<ProfilerProviderDescriptor> Providers { get; set; }

        public string UnityVersion { get; set; }
        public string Error { get; set; }
    }
}