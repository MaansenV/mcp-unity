using System;

namespace McpUnity.Profiler
{
    [Flags]
    public enum ProfilerHistoryCapabilities
    {
        None = 0,

        RecordingControl = 1 << 0,
        CurrentFrameOnly = 1 << 1,
        HistoricalFrames = 1 << 2,
        SelectedFrame = 1 << 3,

        FrameTiming = 1 << 4,
        ThreadHierarchy = 1 << 5,
        SampleHierarchy = 1 << 6,
        SampleMetadata = 1 << 7,

        CpuModule = 1 << 8,
        GpuModule = 1 << 9,
        MemoryModule = 1 << 10,
        Counters = 1 << 11,

        SpikeAnalysis = 1 << 12,
        BaselineComparison = 1 << 13,

        RequiresReflection = 1 << 20,
        UsesInternalUnityApi = 1 << 21
    }
}