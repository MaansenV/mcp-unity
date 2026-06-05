#if UNITY_2021_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Reflection;

namespace McpUnity.Profiler
{
    public sealed class ProfilerFrameDataIteratorProvider : IProfilerHistoryProvider
    {
        private readonly IProfilerDriverReflection _profilerDriver;
        private readonly ProfilerFrameDataIteratorAdapter _iteratorAdapter;

        public string ProviderId => "profiler_frame_data_iterator";
        public string DisplayName => "Profiler Frame Data Iterator";
        public int Priority => 800;
        public bool IsAvailable { get; }
        public ProfilerHistoryCapabilities Capabilities { get; }
        public string AvailabilityReason { get; }

        public ProfilerFrameDataIteratorProvider(IProfilerDriverReflection profilerDriver)
        {
            _profilerDriver = profilerDriver;
            _iteratorAdapter = new ProfilerFrameDataIteratorAdapter();

            IsAvailable = profilerDriver.IsAvailable && _iteratorAdapter.IsAvailable;

            if (IsAvailable)
            {
                // Only advertise capabilities that are actually implemented.
                // TryListFrames and TryGetFrameData currently return "not implemented".
                Capabilities = ProfilerHistoryCapabilities.RecordingControl |
                              ProfilerHistoryCapabilities.HistoricalFrames |
                              ProfilerHistoryCapabilities.FrameTiming |
                              ProfilerHistoryCapabilities.RequiresReflection |
                              ProfilerHistoryCapabilities.UsesInternalUnityApi;

                if (profilerDriver.Discovery.HasMember("selectedFrameIndex"))
                    Capabilities |= ProfilerHistoryCapabilities.SelectedFrame;

                AvailabilityReason = "Available via reflection (status/recording only - frame listing not implemented)";
            }
            else
            {
                Capabilities = ProfilerHistoryCapabilities.None;
                AvailabilityReason = !profilerDriver.IsAvailable ? profilerDriver.AvailabilityReason : _iteratorAdapter.AvailabilityReason;
            }
        }

        public ProfilerProviderDescriptor GetDescriptor()
        {
            return new ProfilerProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = DisplayName,
                Priority = Priority,
                IsAvailable = IsAvailable,
                AvailabilityReason = AvailabilityReason,
                Capabilities = Capabilities,
                UnityVersion = UnityEngine.Application.unityVersion,
                UsesReflection = true
            };
        }

        public ProfilerHistoryStatus GetStatus()
        {
            var status = new ProfilerHistoryStatus
            {
                ProfilerSupported = IsAvailable,
                ActiveProviderId = ProviderId,
                ActiveProviderName = DisplayName,
                Capabilities = Capabilities,
                Providers = new List<ProfilerProviderDescriptor> { GetDescriptor() },
                UnityVersion = UnityEngine.Application.unityVersion
            };

            if (IsAvailable)
            {
                _profilerDriver.TryGetRecordingEnabled(out var recording, out _);
                status.RecordingEnabled = recording;

                _profilerDriver.TryGetFirstFrameIndex(out var first, out _);
                _profilerDriver.TryGetLastFrameIndex(out var last, out _);
                _profilerDriver.TryGetCurrentFrameIndex(out var current, out _);
                _profilerDriver.TryGetSelectedFrameIndex(out var selected, out _);

                status.FrameRange = new ProfilerFrameRange
                {
                    FirstFrameIndex = first,
                    LastFrameIndex = last,
                    CurrentFrameIndex = current,
                    SelectedFrameIndex = selected,
                    FrameCount = Math.Max(0, last - first + 1),
                    HasSelection = selected >= first && selected <= last,
                    IsValid = first <= last
                };
                status.HistoryAvailable = status.FrameRange.IsValid && status.FrameRange.FrameCount > 0;
            }

            return status;
        }

        public bool IsRecordingEnabled()
        {
            if (!_profilerDriver.TryGetRecordingEnabled(out var enabled, out _)) return false;
            return enabled;
        }

        public bool SetRecordingEnabled(bool enabled, out string message)
        {
            return _profilerDriver.TrySetRecordingEnabled(enabled, out message);
        }

        public bool TryGetSelectedFrame(out int frameIndex, out string error)
        {
            if (!_profilerDriver.IsAvailable) { frameIndex = -1; error = "ProfilerDriver not available"; return false; }
            return _profilerDriver.TryGetSelectedFrameIndex(out frameIndex, out error);
        }

        public bool TryGetFrameRange(out ProfilerFrameRange range, out string error)
        {
            range = new ProfilerFrameRange();
            error = null;

            if (!_profilerDriver.TryGetFirstFrameIndex(out var first, out error)) return false;
            if (!_profilerDriver.TryGetLastFrameIndex(out var last, out error)) return false;
            if (!_profilerDriver.TryGetCurrentFrameIndex(out var current, out error)) return false;
            _profilerDriver.TryGetSelectedFrameIndex(out var selected, out _);

            range.FirstFrameIndex = first;
            range.LastFrameIndex = last;
            range.CurrentFrameIndex = current;
            range.SelectedFrameIndex = selected;
            range.FrameCount = Math.Max(0, last - first + 1);
            range.HasSelection = selected >= first && selected <= last;
            range.IsValid = first <= last;

            return true;
        }

        public bool TryListFrames(ProfilerFrameQuery query, out IReadOnlyList<ProfilerFrameSummary> frames, out string error)
        {
            frames = Array.Empty<ProfilerFrameSummary>();
            error = "Frame listing not fully implemented for FrameDataIteratorProvider";
            return false;
        }

        public bool TryGetFrameData(ProfilerFrameQuery query, out ProfilerFrameData frameData, out string error)
        {
            frameData = null;
            error = "Detailed frame data not fully implemented for FrameDataIteratorProvider";
            return false;
        }

        public bool Supports(ProfilerHistoryCapabilities requiredCapabilities)
        {
            return (Capabilities & requiredCapabilities) == requiredCapabilities;
        }

        public void Dispose() { }
    }

    public sealed class ProfilerFrameDataIteratorAdapter
    {
        public bool IsAvailable { get; private set; }
        public string AvailabilityReason { get; private set; }

        private Type _iteratorType;
        private MethodInfo _createMethod;
        private MethodInfo _moveNextMethod;
        private PropertyInfo _currentMethod;
        private MethodInfo _disposeMethod;

        public ProfilerFrameDataIteratorAdapter()
        {
            Discover();
        }

        private void Discover()
        {
            var typeNames = new[]
            {
                "UnityEditorInternal.ProfilerFrameDataIterator, UnityEditor",
                "UnityEditorInternal.ProfilerFrameDataIterator, UnityEditor.CoreModule"
            };

            _iteratorType = null;
            foreach (var typeName in typeNames)
            {
                _iteratorType = Type.GetType(typeName);
                if (_iteratorType != null) break;
            }

            if (_iteratorType == null)
            {
                IsAvailable = false;
                AvailabilityReason = "ProfilerFrameDataIterator type not found";
                return;
            }

            _createMethod = FindMethod("Create", "Open");
            _moveNextMethod = FindMethod("MoveNext");
            _currentMethod = FindProperty("Current");
            _disposeMethod = FindMethod("Dispose");

            IsAvailable = _createMethod != null && _moveNextMethod != null && _currentMethod != null;
            AvailabilityReason = IsAvailable ? "Available" : "Required members missing";
        }

        private PropertyInfo FindProperty(params string[] names)
        {
            foreach (var name in names)
            {
                var prop = _iteratorType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null) return prop;
            }
            return null;
        }

        private MethodInfo FindMethod(params string[] names)
        {
            foreach (var name in names)
            {
                var method = _iteratorType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (method != null) return method;
            }
            return null;
        }

        public bool TryOpenFrame(int frameIndex, out object iterator, out string error)
        {
            iterator = null;
            error = null;
            if (_createMethod == null) { error = "Create method not available"; return false; }
            try { iterator = _createMethod.Invoke(null, new object[] { frameIndex }); return iterator != null; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryMoveNext(object iterator, out bool hasNext, out string error)
        {
            hasNext = false;
            error = null;
            if (_moveNextMethod == null) { error = "MoveNext method not available"; return false; }
            try { hasNext = Convert.ToBoolean(_moveNextMethod.Invoke(iterator, null)); return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryReadCurrentSample(object iterator, out ProfilerSampleData sample, out string error)
        {
            sample = null;
            error = null;
            if (_currentMethod == null) { error = "Current property not available"; return false; }
            try
            {
                var current = _currentMethod.GetValue(iterator);
                sample = ConvertToSampleData(current);
                return sample != null;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryDispose(object iterator, out string error)
        {
            error = null;
            if (iterator == null) return true;
            if (_disposeMethod == null) return true;
            try { _disposeMethod.Invoke(iterator, null); return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        private ProfilerSampleData ConvertToSampleData(object rawSample)
        {
            if (rawSample == null) return null;
            var type = rawSample.GetType();

            return new ProfilerSampleData
            {
                Name = GetProperty<string>(rawSample, type, "name", "Name", "markerName", "MarkerName"),
                Category = GetProperty<string>(rawSample, type, "category", "Category"),
                TotalTimeMs = GetProperty<double>(rawSample, type, "timeMs", "TimeMs", "durationMs", "DurationMs"),
                SelfTimeMs = GetProperty<double>(rawSample, type, "selfTimeMs", "SelfTimeMs", "selfMs", "SelfMs"),
                Depth = GetProperty<int>(rawSample, type, "depth", "Depth"),
                ParentSampleId = GetProperty<int>(rawSample, type, "parentId", "ParentId"),
                CallCount = GetProperty<int>(rawSample, type, "callCount", "CallCount"),
                AllocatedBytes = GetProperty<long>(rawSample, type, "gcAlloc", "GcAlloc", "allocatedBytes", "AllocatedBytes")
            };
        }

        private T GetProperty<T>(object obj, Type type, params string[] names)
        {
            foreach (var name in names)
            {
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.PropertyType == typeof(T))
                {
                    try { return (T)prop.GetValue(obj); } catch { }
                }
            }
            return default;
        }
    }
}
#endif