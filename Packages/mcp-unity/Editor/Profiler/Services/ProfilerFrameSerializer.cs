using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace McpUnity.Profiler
{
    public sealed class ProfilerFrameSerializer
    {
        public JObject SerializeStatus(ProfilerHistoryStatus status)
        {
            var capabilitiesList = new List<string>();
            if (status.Capabilities != ProfilerHistoryCapabilities.None)
            {
                foreach (ProfilerHistoryCapabilities cap in Enum.GetValues(typeof(ProfilerHistoryCapabilities)))
                {
                    if (cap != ProfilerHistoryCapabilities.None && status.Capabilities.HasFlag(cap))
                        capabilitiesList.Add(cap.ToString());
                }
            }

            var providersArray = new JArray();
            if (status.Providers != null)
            {
                foreach (var p in status.Providers)
                {
                    var capsList = new List<string>();
                    if (p.Capabilities != ProfilerHistoryCapabilities.None)
                    {
                        foreach (ProfilerHistoryCapabilities cap in Enum.GetValues(typeof(ProfilerHistoryCapabilities)))
                        {
                            if (cap != ProfilerHistoryCapabilities.None && p.Capabilities.HasFlag(cap))
                                capsList.Add(cap.ToString());
                        }
                    }

                    providersArray.Add(new JObject
                    {
                        ["providerId"] = p.ProviderId,
                        ["displayName"] = p.DisplayName,
                        ["priority"] = p.Priority,
                        ["isAvailable"] = p.IsAvailable,
                        ["availabilityReason"] = p.AvailabilityReason,
                        ["capabilities"] = new JArray(capsList),
                        ["unityVersion"] = p.UnityVersion,
                        ["usesReflection"] = p.UsesReflection
                    });
                }
            }

            var frameRange = status.FrameRange;
            var frameRangeObj = new JObject
            {
                ["firstFrameIndex"] = frameRange.FirstFrameIndex,
                ["lastFrameIndex"] = frameRange.LastFrameIndex,
                ["selectedFrameIndex"] = frameRange.SelectedFrameIndex,
                ["currentFrameIndex"] = frameRange.CurrentFrameIndex,
                ["frameCount"] = frameRange.FrameCount,
                ["hasSelection"] = frameRange.HasSelection,
                ["isValid"] = frameRange.IsValid
            };

            return new JObject
            {
                ["profilerSupported"] = status.ProfilerSupported,
                ["recordingEnabled"] = status.RecordingEnabled,
                ["historyAvailable"] = status.HistoryAvailable,
                ["activeProviderId"] = status.ActiveProviderId,
                ["activeProviderName"] = status.ActiveProviderName,
                ["capabilities"] = new JArray(capabilitiesList),
                ["frameRange"] = frameRangeObj,
                ["providers"] = providersArray,
                ["unityVersion"] = status.UnityVersion,
                ["error"] = status.Error
            };
        }

        public JObject SerializeFrameSummary(ProfilerFrameSummary frame)
        {
            return new JObject
            {
                ["frameIndex"] = frame.FrameIndex,
                ["frameTimeMs"] = Math.Round(frame.FrameTimeMs, 3),
                ["cpuTimeMs"] = Math.Round(frame.CpuTimeMs, 3),
                ["gpuTimeMs"] = Math.Round(frame.GpuTimeMs, 3),
                ["isSelected"] = frame.IsSelected,
                ["isCurrent"] = frame.IsCurrent,
                ["isSpike"] = frame.IsSpike,
                ["primaryThreadName"] = frame.PrimaryThreadName,
                ["topMarkerName"] = frame.TopMarkerName,
                ["topMarkerTimeMs"] = Math.Round(frame.TopMarkerTimeMs, 3)
            };
        }

        public JArray SerializeFrameSummaries(IReadOnlyList<ProfilerFrameSummary> frames, ProfilerSerializationOptions options, out bool wasTruncated, out string truncationReason)
        {
            wasTruncated = false;
            truncationReason = null;

            var array = new JArray();
            long estimatedBytes = 0;
            var maxBytes = options?.MaxResponseBytes ?? 512 * 1024;

            foreach (var frame in frames)
            {
                var obj = SerializeFrameSummary(frame);
                var json = obj.ToString();
                estimatedBytes += json.Length;

                if (estimatedBytes > maxBytes)
                {
                    wasTruncated = true;
                    truncationReason = $"Response size limit ({maxBytes} bytes) exceeded";
                    break;
                }

                array.Add(obj);
            }

            return array;
        }

        public JObject SerializeFrameData(ProfilerFrameData frameData, ProfilerSerializationOptions options)
        {
            var threadsArray = new JArray();
            int totalSamples = 0;
            var maxSamples = options?.MaxSamplesTotal ?? 5000;
            var maxStringLength = options?.MaxStringLength ?? 512;

            if (frameData.Threads != null)
            {
                foreach (var thread in frameData.Threads)
                {
                    var samplesArray = new JArray();
                    bool threadTruncated = false;

                    if (thread.Samples != null)
                    {
                        foreach (var sample in thread.Samples)
                        {
                            if (totalSamples >= maxSamples)
                            {
                                threadTruncated = true;
                                break;
                            }

                            samplesArray.Add(SerializeSample(sample, maxStringLength));
                            totalSamples++;
                        }
                    }

                    threadsArray.Add(new JObject
                    {
                        ["threadIndex"] = thread.ThreadIndex,
                        ["threadName"] = TruncateString(thread.ThreadName, maxStringLength),
                        ["groupName"] = TruncateString(thread.GroupName, maxStringLength),
                        ["totalTimeMs"] = Math.Round(thread.TotalTimeMs, 3),
                        ["selfTimeMs"] = Math.Round(thread.SelfTimeMs, 3),
                        ["samples"] = samplesArray,
                        ["wasTruncated"] = threadTruncated
                    });
                }
            }

            var countersObj = new JObject();
            if (frameData.Counters != null)
            {
                foreach (var kvp in frameData.Counters)
                {
                    countersObj[kvp.Key] = Math.Round(kvp.Value, 3);
                }
            }

            return new JObject
            {
                ["frameIndex"] = frameData.FrameIndex,
                ["frameTimeMs"] = Math.Round(frameData.FrameTimeMs, 3),
                ["cpuTimeMs"] = Math.Round(frameData.CpuTimeMs, 3),
                ["gpuTimeMs"] = Math.Round(frameData.GpuTimeMs, 3),
                ["editorLoopTimeMs"] = Math.Round(frameData.EditorLoopTimeMs, 3),
                ["isSelected"] = frameData.IsSelected,
                ["isCurrent"] = frameData.IsCurrent,
                ["providerId"] = frameData.ProviderId,
                ["capabilitiesUsed"] = frameData.CapabilitiesUsed.ToString(),
                ["threads"] = threadsArray,
                ["counters"] = countersObj,
                ["wasTruncated"] = frameData.WasTruncated,
                ["truncationReason"] = frameData.TruncationReason
            };
        }

        private JObject SerializeSample(ProfilerSampleData sample, int maxStringLength)
        {
            var childrenArray = new JArray();
            if (sample.Children != null)
            {
                foreach (var child in sample.Children)
                {
                    childrenArray.Add(SerializeSample(child, maxStringLength));
                }
            }

            var metadataObj = new JObject();
            if (sample.Metadata != null)
            {
                foreach (var kvp in sample.Metadata)
                {
                    metadataObj[kvp.Key] = TruncateString(kvp.Value, maxStringLength);
                }
            }

            return new JObject
            {
                ["sampleId"] = sample.SampleId,
                ["parentSampleId"] = sample.ParentSampleId,
                ["depth"] = sample.Depth,
                ["name"] = TruncateString(sample.Name, maxStringLength),
                ["category"] = TruncateString(sample.Category, maxStringLength),
                ["threadName"] = TruncateString(sample.ThreadName, maxStringLength),
                ["totalTimeMs"] = Math.Round(sample.TotalTimeMs, 3),
                ["selfTimeMs"] = Math.Round(sample.SelfTimeMs, 3),
                ["allocatedBytes"] = sample.AllocatedBytes,
                ["callCount"] = sample.CallCount,
                ["metadata"] = metadataObj,
                ["children"] = childrenArray
            };
        }

        public JObject SerializeSpikeAnalysis(ProfilerSpikeAnalysisResult result, ProfilerSerializationOptions options)
        {
            var spikesArray = new JArray();
            foreach (var spike in result.Spikes)
            {
                spikesArray.Add(new JObject
                {
                    ["frameIndex"] = spike.FrameIndex,
                    ["frameTimeMs"] = Math.Round(spike.FrameTimeMs, 3),
                    ["baselineFrameTimeMs"] = Math.Round(spike.BaselineFrameTimeMs, 3),
                    ["overBaselineMs"] = Math.Round(spike.OverBaselineMs, 3),
                    ["multiplier"] = Math.Round(spike.Multiplier, 2),
                    ["suspectedMarkerName"] = spike.SuspectedMarkerName,
                    ["suspectedMarkerTimeMs"] = Math.Round(spike.SuspectedMarkerTimeMs, 3),
                    ["threadName"] = spike.ThreadName,
                    ["frameSummary"] = spike.FrameSummary != null ? SerializeFrameSummary(spike.FrameSummary) : null
                });
            }

            return new JObject
            {
                ["analyzedRange"] = new JObject
                {
                    ["firstFrameIndex"] = result.AnalyzedRange.FirstFrameIndex,
                    ["lastFrameIndex"] = result.AnalyzedRange.LastFrameIndex,
                    ["frameCount"] = result.AnalyzedRange.FrameCount
                },
                ["baseline"] = new JObject
                {
                    ["averageFrameTimeMs"] = Math.Round(result.Baseline.AverageFrameTimeMs, 3),
                    ["medianFrameTimeMs"] = Math.Round(result.Baseline.MedianFrameTimeMs, 3),
                    ["minFrameTimeMs"] = Math.Round(result.Baseline.MinFrameTimeMs, 3),
                    ["maxFrameTimeMs"] = Math.Round(result.Baseline.MaxFrameTimeMs, 3),
                    ["standardDeviationMs"] = Math.Round(result.Baseline.StandardDeviationMs, 3),
                    ["sampleCount"] = result.Baseline.SampleCount
                },
                ["spikes"] = spikesArray,
                ["framesAnalyzed"] = result.FramesAnalyzed,
                ["spikeCount"] = result.SpikeCount,
                ["worstFrameTimeMs"] = Math.Round(result.WorstFrameTimeMs, 3),
                ["worstFrameIndex"] = result.WorstFrameIndex,
                ["wasTruncated"] = result.WasTruncated,
                ["message"] = result.Message
            };
        }

        public JObject CreateSuccessResponse(string message, JObject data)
        {
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = message,
                ["data"] = data
            };
        }

        public JObject CreateErrorResponse(string message, string errorCode, JObject details = null)
        {
            var error = new JObject
            {
                ["success"] = false,
                ["type"] = "text",
                ["message"] = message,
                ["errorCode"] = errorCode
            };

            if (details != null)
                error["details"] = details;

            return error;
        }

        private string TruncateString(string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str.Length <= maxLength ? str : str.Substring(0, maxLength) + "...";
        }
    }
}