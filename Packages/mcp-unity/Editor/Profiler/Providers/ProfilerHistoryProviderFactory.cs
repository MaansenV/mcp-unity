using System;
using System.Collections.Generic;
using System.Linq;

namespace McpUnity.Profiler
{
    public sealed class ProfilerHistoryProviderFactory : IDisposable
    {
        private readonly IProfilerDriverReflection _profilerDriver;
        private readonly IRawFrameDataViewAdapter _rawFrameDataViewAdapter;
        private readonly List<IProfilerHistoryProvider> _providers;

        public ProfilerHistoryProviderFactory()
        {
            _profilerDriver = new ProfilerDriverReflection();
            _rawFrameDataViewAdapter = CreateRawFrameDataViewAdapter();
            _providers = CreateProviders();
        }

        private IRawFrameDataViewAdapter CreateRawFrameDataViewAdapter()
        {
#if UNITY_2022_3_OR_NEWER
            return new RawFrameDataViewAdapter(_profilerDriver);
#else
            return new NullRawFrameDataViewAdapter();
#endif
        }

        private List<IProfilerHistoryProvider> CreateProviders()
        {
            var providers = new List<IProfilerHistoryProvider>();

#if UNITY_2022_3_OR_NEWER
            providers.Add(new RawFrameDataViewProvider(_profilerDriver, _rawFrameDataViewAdapter));
#endif

#if UNITY_2021_3_OR_NEWER
            providers.Add(new ProfilerFrameDataIteratorProvider(_profilerDriver));
#endif

            providers.Add(new EditorProfilerDriverProvider(_profilerDriver));
            providers.Add(new LiveRecorderProvider());

            return providers.OrderByDescending(p => p.Priority).ToList();
        }

        public IReadOnlyList<IProfilerHistoryProvider> GetProviders()
        {
            return _providers.AsReadOnly();
        }

        public IProfilerHistoryProvider CreateBestProvider()
        {
            return CreateBestProvider(ProfilerHistoryCapabilities.None, out _);
        }

        public IProfilerHistoryProvider CreateBestProvider(out IReadOnlyList<ProfilerProviderDescriptor> diagnostics)
        {
            return CreateBestProvider(ProfilerHistoryCapabilities.None, out diagnostics);
        }

        public IProfilerHistoryProvider CreateBestProvider(ProfilerHistoryCapabilities requiredCapabilities, out IReadOnlyList<ProfilerProviderDescriptor> diagnostics)
        {
            var descriptors = new List<ProfilerProviderDescriptor>();

            foreach (var provider in _providers)
            {
                descriptors.Add(provider.GetDescriptor());

                if (provider.IsAvailable && provider.Supports(requiredCapabilities))
                {
                    diagnostics = descriptors.AsReadOnly();
                    return provider;
                }
            }

            var fallback = _providers.FirstOrDefault(p => p.IsAvailable) ?? _providers.Last();
            diagnostics = descriptors.AsReadOnly();
            return fallback;
        }

        public IReadOnlyList<ProfilerProviderDescriptor> DetectProviderDescriptors()
        {
            return _providers.Select(p => p.GetDescriptor()).ToList().AsReadOnly();
        }

        public void Dispose()
        {
            foreach (var p in _providers)
            {
                if (p is IDisposable d)
                {
                    try { d.Dispose(); }
                    catch { /* swallow disposal errors during shutdown */ }
                }
            }
            _providers.Clear();
        }

        private class NullRawFrameDataViewAdapter : IRawFrameDataViewAdapter
        {
            public bool IsAvailable => false;
            public string AvailabilityReason => "Unity version < 2022.3";

            public bool TryOpenFrame(int frameIndex, int threadIndex, out object rawFrameDataView, out string error)
            {
                rawFrameDataView = null; error = "Not available"; return false;
            }

            public bool TryGetThreadCount(int frameIndex, out int threadCount, out string error)
            {
                threadCount = 0; error = "Not available"; return false;
            }

            public bool TryGetThreadName(object rawFrameDataView, out string threadName, out string groupName, out string error)
            {
                threadName = null; groupName = null; error = "Not available"; return false;
            }

            public bool TryGetSampleCount(object rawFrameDataView, out int sampleCount, out string error)
            {
                sampleCount = 0; error = "Not available"; return false;
            }

            public bool TryReadSample(object rawFrameDataView, int sampleIndex, out ProfilerSampleData sample, out string error)
            {
                sample = null; error = "Not available"; return false;
            }

            public bool TryDisposeView(object rawFrameDataView, out string error)
            {
                error = null; return true;
            }
        }
    }
}