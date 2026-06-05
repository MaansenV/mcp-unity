using System;

namespace McpUnity.Profiler
{
    public sealed class FrameRangeResolver
    {
        public bool TryResolveFrame(FrameReference reference, ProfilerFrameRange range, out int frameIndex, out string error)
        {
            frameIndex = -1;
            error = null;

            if (!range.IsValid) { error = "Invalid frame range"; return false; }

            switch (reference.Kind)
            {
                case FrameReferenceKind.Absolute:
                    frameIndex = reference.Value;
                    break;

                case FrameReferenceKind.Current:
                    frameIndex = range.CurrentFrameIndex;
                    break;

                case FrameReferenceKind.Selected:
                    if (!range.HasSelection) { error = "No frame selected in profiler"; return false; }
                    frameIndex = range.SelectedFrameIndex;
                    break;

                case FrameReferenceKind.Latest:
                    frameIndex = range.LastFrameIndex;
                    break;

                case FrameReferenceKind.Oldest:
                    frameIndex = range.FirstFrameIndex;
                    break;

                case FrameReferenceKind.RelativeToCurrent:
                    frameIndex = range.CurrentFrameIndex + reference.Value;
                    break;

                case FrameReferenceKind.RelativeToSelected:
                    if (!range.HasSelection) { error = "No frame selected in profiler"; return false; }
                    frameIndex = range.SelectedFrameIndex + reference.Value;
                    break;

                case FrameReferenceKind.RelativeToLatest:
                    frameIndex = range.LastFrameIndex + reference.Value;
                    break;

                default:
                    error = $"Unknown FrameReferenceKind: {reference.Kind}";
                    return false;
            }

            if (!range.Contains(frameIndex))
            {
                error = $"Frame index {frameIndex} is outside available range [{range.FirstFrameIndex}..{range.LastFrameIndex}]";
                return false;
            }

            return true;
        }

        public bool TryResolveQueryRange(ProfilerFrameQuery query, ProfilerFrameRange availableRange, out ProfilerFrameRange resolvedRange, out string error)
        {
            resolvedRange = new ProfilerFrameRange();
            error = null;

            if (!availableRange.IsValid) { error = "Invalid available frame range"; return false; }

            int startFrame, endFrame;

            if (query.StartFrame.Kind != FrameReferenceKind.Absolute && query.StartFrame.Kind != FrameReferenceKind.Current &&
                query.StartFrame.Kind != FrameReferenceKind.Selected && query.StartFrame.Kind != FrameReferenceKind.Latest &&
                query.StartFrame.Kind != FrameReferenceKind.Oldest && query.StartFrame.Kind != FrameReferenceKind.RelativeToCurrent &&
                query.StartFrame.Kind != FrameReferenceKind.RelativeToSelected && query.StartFrame.Kind != FrameReferenceKind.RelativeToLatest)
            {
                error = "Invalid StartFrame reference kind";
                return false;
            }

            if (query.EndFrame.Kind != FrameReferenceKind.Absolute && query.EndFrame.Kind != FrameReferenceKind.Current &&
                query.EndFrame.Kind != FrameReferenceKind.Selected && query.EndFrame.Kind != FrameReferenceKind.Latest &&
                query.EndFrame.Kind != FrameReferenceKind.Oldest && query.EndFrame.Kind != FrameReferenceKind.RelativeToCurrent &&
                query.EndFrame.Kind != FrameReferenceKind.RelativeToSelected && query.EndFrame.Kind != FrameReferenceKind.RelativeToLatest)
            {
                error = "Invalid EndFrame reference kind";
                return false;
            }

            if (!TryResolveFrame(query.StartFrame, availableRange, out startFrame, out error)) return false;
            if (!TryResolveFrame(query.EndFrame, availableRange, out endFrame, out error)) return false;

            if (startFrame > endFrame)
            {
                error = $"StartFrame ({startFrame}) cannot be greater than EndFrame ({endFrame})";
                return false;
            }

            var frameCount = endFrame - startFrame + 1;
            var maxFrames = Math.Max(1, query.MaxFrames);

            if (frameCount > maxFrames)
            {
                startFrame = endFrame - maxFrames + 1;
                frameCount = maxFrames;
            }

            resolvedRange.FirstFrameIndex = startFrame;
            resolvedRange.LastFrameIndex = endFrame;
            resolvedRange.FrameCount = frameCount;
            resolvedRange.CurrentFrameIndex = availableRange.CurrentFrameIndex;
            resolvedRange.SelectedFrameIndex = availableRange.SelectedFrameIndex;
            resolvedRange.HasSelection = availableRange.HasSelection;
            resolvedRange.IsValid = true;

            return true;
        }
    }
}