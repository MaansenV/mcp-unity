namespace McpUnity.Profiler
{
    public class ProfilerSerializationOptions
    {
        public int MaxResponseBytes { get; set; } = 512 * 1024;
        public int MaxSamplesTotal { get; set; } = 5000;
        public int MaxStringLength { get; set; } = 512;
        public bool IncludeRawCapabilityFlags { get; set; } = true;
        public bool IncludeProviderDiagnostics { get; set; } = false;
    }
}