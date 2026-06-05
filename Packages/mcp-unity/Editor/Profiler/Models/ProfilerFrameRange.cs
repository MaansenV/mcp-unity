using System;

namespace McpUnity.Profiler
{
    public struct ProfilerFrameRange
    {
        public int FirstFrameIndex { get; set; }
        public int LastFrameIndex { get; set; }
        public int SelectedFrameIndex { get; set; }
        public int CurrentFrameIndex { get; set; }
        public int FrameCount { get; set; }
        public bool HasSelection { get; set; }
        public bool IsValid { get; set; }

        public bool Contains(int frameIndex)
        {
            return IsValid && frameIndex >= FirstFrameIndex && frameIndex <= LastFrameIndex;
        }

        public int Clamp(int frameIndex)
        {
            if (!IsValid) return frameIndex;
            return Math.Max(FirstFrameIndex, Math.Min(LastFrameIndex, frameIndex));
        }
    }
}