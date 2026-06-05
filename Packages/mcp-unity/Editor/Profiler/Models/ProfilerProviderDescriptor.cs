namespace McpUnity.Profiler
{
    public class ProfilerProviderDescriptor
    {
        public string ProviderId { get; set; }
        public string DisplayName { get; set; }
        public int Priority { get; set; }
        public bool IsAvailable { get; set; }
        public string AvailabilityReason { get; set; }
        public ProfilerHistoryCapabilities Capabilities { get; set; }
        public string UnityVersion { get; set; }
        public bool UsesReflection { get; set; }
    }
}