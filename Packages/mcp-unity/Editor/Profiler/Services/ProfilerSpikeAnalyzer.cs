using System;
using System.Collections.Generic;
using System.Linq;

namespace McpUnity.Profiler
{
    public sealed class ProfilerSpikeAnalyzer
    {
        public bool TryAnalyze(
            IReadOnlyList<ProfilerFrameSummary> frameSummaries,
            ProfilerFrameQuery query,
            out ProfilerSpikeAnalysisResult result,
            out string error)
        {
            result = null;
            error = null;

            if (frameSummaries == null || frameSummaries.Count == 0)
            {
                error = "No frame summaries provided";
                return false;
            }

            var baseline = CalculateBaseline(frameSummaries);
            var spikes = new List<ProfilerSpikeInfo>();
            double worstFrameTime = 0;
            int worstFrameIndex = -1;

            var thresholdMs = query.SpikeThresholdMs;
            var thresholdMultiplier = query.SpikeThresholdMultiplier;

            foreach (var frame in frameSummaries)
            {
                if (IsSpike(frame, baseline, thresholdMs, thresholdMultiplier))
                {
                    var spike = new ProfilerSpikeInfo
                    {
                        FrameIndex = frame.FrameIndex,
                        FrameTimeMs = frame.FrameTimeMs,
                        BaselineFrameTimeMs = baseline.MedianFrameTimeMs,
                        OverBaselineMs = frame.FrameTimeMs - baseline.MedianFrameTimeMs,
                        Multiplier = baseline.MedianFrameTimeMs > 0 ? frame.FrameTimeMs / baseline.MedianFrameTimeMs : 0,
                        SuspectedMarkerName = frame.TopMarkerName,
                        SuspectedMarkerTimeMs = frame.TopMarkerTimeMs,
                        ThreadName = frame.PrimaryThreadName,
                        FrameSummary = frame
                    };
                    spikes.Add(spike);

                    if (frame.FrameTimeMs > worstFrameTime)
                    {
                        worstFrameTime = frame.FrameTimeMs;
                        worstFrameIndex = frame.FrameIndex;
                    }
                }
            }

            result = new ProfilerSpikeAnalysisResult
            {
                AnalyzedRange = new ProfilerFrameRange
                {
                    FirstFrameIndex = frameSummaries.Min(f => f.FrameIndex),
                    LastFrameIndex = frameSummaries.Max(f => f.FrameIndex),
                    FrameCount = frameSummaries.Count
                },
                Baseline = baseline,
                Spikes = spikes,
                FramesAnalyzed = frameSummaries.Count,
                SpikeCount = spikes.Count,
                WorstFrameTimeMs = worstFrameTime,
                WorstFrameIndex = worstFrameIndex,
                Message = spikes.Count > 0 ? $"Found {spikes.Count} spike(s)" : "No spikes detected"
            };

            return true;
        }

        public ProfilerBaselineInfo CalculateBaseline(IReadOnlyList<ProfilerFrameSummary> frameSummaries)
        {
            if (frameSummaries == null || frameSummaries.Count == 0)
                return new ProfilerBaselineInfo();

            var times = frameSummaries.Select(f => f.FrameTimeMs).OrderBy(t => t).ToArray();
            var count = times.Length;

            double sum = 0;
            double min = times[0];
            double max = times[count - 1];

            foreach (var t in times) sum += t;
            double avg = sum / count;

            double median;
            if (count % 2 == 0)
                median = (times[count / 2 - 1] + times[count / 2]) / 2.0;
            else
                median = times[count / 2];

            double varianceSum = 0;
            foreach (var t in times)
            {
                var diff = t - avg;
                varianceSum += diff * diff;
            }
            double stdDev = Math.Sqrt(varianceSum / count);

            return new ProfilerBaselineInfo
            {
                AverageFrameTimeMs = avg,
                MedianFrameTimeMs = median,
                MinFrameTimeMs = min,
                MaxFrameTimeMs = max,
                StandardDeviationMs = stdDev,
                SampleCount = count
            };
        }

        public bool IsSpike(ProfilerFrameSummary frame, ProfilerBaselineInfo baseline, double thresholdMs, double thresholdMultiplier)
        {
            if (frame.FrameTimeMs >= thresholdMs) return true;
            if (baseline.MedianFrameTimeMs > 0 && frame.FrameTimeMs >= baseline.MedianFrameTimeMs * thresholdMultiplier) return true;
            return false;
        }
    }
}