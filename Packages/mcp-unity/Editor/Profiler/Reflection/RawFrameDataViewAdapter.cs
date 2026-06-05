#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Reflection;

namespace McpUnity.Profiler
{
    public sealed class RawFrameDataViewAdapter : IRawFrameDataViewAdapter
    {
        private Type _rawFrameDataViewType;
        private MethodInfo _openFrameMethod;
        private PropertyInfo _threadCountProp;
        private MethodInfo _getThreadNameMethod;
        private PropertyInfo _sampleCountProp;
        private MethodInfo _getSampleMethod;
        private MethodInfo _disposeMethod;

        public bool IsAvailable { get; private set; }
        public string AvailabilityReason { get; private set; }

        public RawFrameDataViewAdapter(IProfilerDriverReflection profilerDriver)
        {
            DiscoverRawFrameDataViewMembers(profilerDriver);
        }

        private void DiscoverRawFrameDataViewMembers(IProfilerDriverReflection profilerDriver)
        {
            if (!profilerDriver.IsAvailable)
            {
                IsAvailable = false;
                AvailabilityReason = "ProfilerDriver not available";
                return;
            }

            var typeNames = new[]
            {
                "UnityEditorInternal.Profiling.RawFrameDataView, UnityEditor",
                "UnityEditorInternal.Profiling.RawFrameDataView, UnityEditor.CoreModule",
                "Unity.Profiling.Editor.RawFrameDataView, UnityEditor"
            };

            _rawFrameDataViewType = null;
            foreach (var typeName in typeNames)
            {
                _rawFrameDataViewType = Type.GetType(typeName);
                if (_rawFrameDataViewType != null) break;
            }

            if (_rawFrameDataViewType == null)
            {
                IsAvailable = false;
                AvailabilityReason = "RawFrameDataView type not found";
                return;
            }

            _openFrameMethod = FindMethod("OpenFrame", "Create");
            _threadCountProp = FindProperty("threadCount", "ThreadCount");
            _getThreadNameMethod = FindMethod("GetThreadName", "GetThreadGroupName");
            _sampleCountProp = FindProperty("sampleCount", "SampleCount");
            _getSampleMethod = FindMethod("GetSample", "ReadSample");
            _disposeMethod = FindMethod("Dispose", "Close");

            IsAvailable = _openFrameMethod != null && _sampleCountProp != null && _getSampleMethod != null;
            AvailabilityReason = IsAvailable ? "Available" : "Required members missing";
        }

        private PropertyInfo FindProperty(params string[] names)
        {
            foreach (var name in names)
            {
                var prop = _rawFrameDataViewType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null) return prop;
            }
            return null;
        }

        private MethodInfo FindMethod(params string[] names)
        {
            foreach (var name in names)
            {
                var method = _rawFrameDataViewType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (method != null) return method;
            }
            return null;
        }

        public bool TryOpenFrame(int frameIndex, int threadIndex, out object rawFrameDataView, out string error)
        {
            rawFrameDataView = null;
            error = null;
            if (_openFrameMethod == null) { error = "OpenFrame method not available"; return false; }
            try
            {
                rawFrameDataView = _openFrameMethod.Invoke(null, new object[] { frameIndex, threadIndex });
                return rawFrameDataView != null;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryGetThreadCount(int frameIndex, out int threadCount, out string error)
        {
            threadCount = 0;
            error = null;
            if (_threadCountProp == null) { error = "threadCount property not available"; return false; }
            try
            {
                var view = OpenFrameForThreadCount(frameIndex);
                try
                {
                    if (view == null) { error = "Failed to open frame for thread count"; return false; }
                    threadCount = Convert.ToInt32(_threadCountProp.GetValue(view));
                    return true;
                }
                finally
                {
                    TryDisposeView(view, out _);
                }
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        private object OpenFrameForThreadCount(int frameIndex)
        {
            if (_openFrameMethod == null) return null;
            try { return _openFrameMethod.Invoke(null, new object[] { frameIndex, 0 }); }
            catch { return null; }
        }

        public bool TryGetThreadName(object rawFrameDataView, out string threadName, out string groupName, out string error)
        {
            threadName = null;
            groupName = null;
            error = null;
            if (rawFrameDataView == null) { error = "View is null"; return false; }
            if (_getThreadNameMethod == null) { error = "GetThreadName method not available"; return false; }
            try
            {
                var result = _getThreadNameMethod.Invoke(rawFrameDataView, null);
                if (result is string s) { threadName = s; return true; }
                if (result is object[] arr && arr.Length >= 2) { threadName = arr[0]?.ToString(); groupName = arr[1]?.ToString(); return true; }
                error = "Unexpected return type";
                return false;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryGetSampleCount(object rawFrameDataView, out int sampleCount, out string error)
        {
            sampleCount = 0;
            error = null;
            if (rawFrameDataView == null) { error = "View is null"; return false; }
            if (_sampleCountProp == null) { error = "sampleCount property not available"; return false; }
            try { sampleCount = Convert.ToInt32(_sampleCountProp.GetValue(rawFrameDataView)); return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryReadSample(object rawFrameDataView, int sampleIndex, out ProfilerSampleData sample, out string error)
        {
            sample = null;
            error = null;
            if (rawFrameDataView == null) { error = "View is null"; return false; }
            if (_getSampleMethod == null) { error = "GetSample method not available"; return false; }
            try
            {
                var result = _getSampleMethod.Invoke(rawFrameDataView, new object[] { sampleIndex });
                sample = ConvertToSampleData(result, sampleIndex);
                return sample != null;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        private ProfilerSampleData ConvertToSampleData(object rawSample, int sampleIndex = -1)
        {
            if (rawSample == null) return null;
            var type = rawSample.GetType();

            var sample = new ProfilerSampleData
            {
                SampleId = sampleIndex >= 0 ? sampleIndex : GetProperty<int>(rawSample, type, "sampleId", "SampleId", "id", "Id"),
                Name = GetProperty<string>(rawSample, type, "name", "Name", "markerName", "MarkerName"),
                Category = GetProperty<string>(rawSample, type, "category", "Category", "categoryName", "CategoryName"),
                TotalTimeMs = GetProperty<double>(rawSample, type, "timeMs", "TimeMs", "durationMs", "DurationMs"),
                SelfTimeMs = GetProperty<double>(rawSample, type, "selfTimeMs", "SelfTimeMs", "selfMs", "SelfMs"),
                Depth = GetProperty<int>(rawSample, type, "depth", "Depth"),
                ParentSampleId = GetProperty<int>(rawSample, type, "parentId", "ParentId", "parentSampleId", "ParentSampleId"),
                CallCount = GetProperty<int>(rawSample, type, "callCount", "CallCount"),
                AllocatedBytes = GetProperty<long>(rawSample, type, "gcAlloc", "GcAlloc", "allocatedBytes", "AllocatedBytes")
            };

            return sample;
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

        public bool TryDisposeView(object rawFrameDataView, out string error)
        {
            error = null;
            if (rawFrameDataView == null) return true;
            if (_disposeMethod == null) return true;
            try { _disposeMethod.Invoke(rawFrameDataView, null); return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}
#endif