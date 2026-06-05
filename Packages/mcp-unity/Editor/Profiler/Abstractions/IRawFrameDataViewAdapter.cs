using System.Collections.Generic;

namespace McpUnity.Profiler
{
    public interface IRawFrameDataViewAdapter
    {
        bool IsAvailable { get; }
        string AvailabilityReason { get; }

        bool TryOpenFrame(
            int frameIndex,
            int threadIndex,
            out object rawFrameDataView,
            out string error);

        bool TryGetThreadCount(
            int frameIndex,
            out int threadCount,
            out string error);

        bool TryGetThreadName(
            object rawFrameDataView,
            out string threadName,
            out string groupName,
            out string error);

        bool TryGetSampleCount(
            object rawFrameDataView,
            out int sampleCount,
            out string error);

        bool TryReadSample(
            object rawFrameDataView,
            int sampleIndex,
            out ProfilerSampleData sample,
            out string error);

        bool TryDisposeView(object rawFrameDataView, out string error);
    }
}