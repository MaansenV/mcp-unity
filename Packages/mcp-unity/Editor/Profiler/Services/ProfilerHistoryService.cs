using System;
using System.Collections.Generic;
using System.Linq;

namespace McpUnity.Profiler
{
    public sealed class ProfilerHistoryService : IDisposable
    {
        private static readonly Lazy<ProfilerHistoryService> _instance = new Lazy<ProfilerHistoryService>(() => new ProfilerHistoryService());
        public static ProfilerHistoryService Instance => _instance.Value;

        private readonly ProfilerHistoryProviderFactory _factory;
        private readonly object _sync = new object();
        private IProfilerHistoryProvider _activeProvider;
        private IReadOnlyList<ProfilerProviderDescriptor> _lastDiagnostics;
        private bool _disposed;

        private ProfilerHistoryService()
        {
            _factory = new ProfilerHistoryProviderFactory();
            RefreshProvider();
        }

        public IProfilerHistoryProvider ActiveProvider => _activeProvider;

        public ProfilerHistoryStatus GetStatus()
        {
            var provider = GetOrCreateProvider();
            return provider?.GetStatus() ?? new ProfilerHistoryStatus { ProfilerSupported = false, Error = "No provider available" };
        }

        public bool SetRecordingEnabled(bool enabled, out ProfilerHistoryStatus status, out string error)
        {
            var provider = GetOrCreateProvider();

            if (provider == null)
            {
                error = "No provider available";
                status = new ProfilerHistoryStatus { ProfilerSupported = false, Error = error };
                return false;
            }

            var success = provider.SetRecordingEnabled(enabled, out error);
            status = provider.GetStatus();
            return success;
        }

        public bool TryGetSelectedFrame(out ProfilerFrameSummary frame, out string error)
        {
            frame = null;
            error = null;

            var provider = GetOrCreateProvider();
            if (provider == null) { error = "No provider available"; return false; }

            if (!provider.TryGetSelectedFrame(out var frameIndex, out error)) return false;

            var query = new ProfilerFrameQuery { Frame = FrameReference.Absolute(frameIndex) }.Normalize();
            if (!provider.TryGetFrameData(query, out var frameData, out error)) return false;

            // Safely collect all samples (null-safe)
            var allSamples = (frameData.Threads ?? Array.Empty<ProfilerThreadData>())
                .Where(t => t.Samples != null)
                .SelectMany(t => t.Samples)
                .ToList();

            var topSample = allSamples.OrderByDescending(s => s.SelfTimeMs).FirstOrDefault();
            var topTime = allSamples.Count > 0 ? allSamples.Max(s => s.SelfTimeMs) : 0;

            frame = new ProfilerFrameSummary
            {
                FrameIndex = frameData.FrameIndex,
                FrameTimeMs = frameData.FrameTimeMs,
                CpuTimeMs = frameData.CpuTimeMs,
                GpuTimeMs = frameData.GpuTimeMs,
                IsSelected = true,
                IsCurrent = frameData.IsCurrent,
                PrimaryThreadName = frameData.Threads?.FirstOrDefault()?.ThreadName,
                TopMarkerName = topSample?.Name,
                TopMarkerTimeMs = topTime
            };

            return true;
        }

        public bool TryListFrames(ProfilerFrameQuery query, out IReadOnlyList<ProfilerFrameSummary> frames, out ProfilerHistoryStatus status, out string error)
        {
            frames = null;
            status = null;
            error = null;

            var provider = GetOrCreateProvider();
            if (provider == null) { error = "No provider available"; return false; }

            status = provider.GetStatus();

            var normalized = (query ?? new ProfilerFrameQuery()).Normalize();
            return provider.TryListFrames(normalized, out frames, out error);
        }

        public bool TryGetFrameData(ProfilerFrameQuery query, out ProfilerFrameData frameData, out ProfilerHistoryStatus status, out string error)
        {
            frameData = null;
            status = null;
            error = null;

            var provider = GetOrCreateProvider();
            if (provider == null) { error = "No provider available"; return false; }

            status = provider.GetStatus();

            var normalized = (query ?? new ProfilerFrameQuery()).Normalize();
            return provider.TryGetFrameData(normalized, out frameData, out error);
        }

        public bool TryAnalyzeSpikes(ProfilerFrameQuery query, out ProfilerSpikeAnalysisResult result, out ProfilerHistoryStatus status, out string error)
        {
            result = null;
            status = null;
            error = null;

            var provider = GetOrCreateProvider();
            if (provider == null) { error = "No provider available"; return false; }

            status = provider.GetStatus();

            var normalized = (query ?? new ProfilerFrameQuery()).Normalize();
            if (!provider.TryListFrames(normalized, out var frames, out error)) return false;

            var analyzer = new ProfilerSpikeAnalyzer();
            return analyzer.TryAnalyze(frames, normalized, out result, out error);
        }

        public void RefreshProvider()
        {
            lock (_sync)
            {
                if (_disposed) return;

                var newProvider = _factory.CreateBestProvider(ProfilerHistoryCapabilities.None, out _lastDiagnostics);

                // Dispose the old provider if it changed and is disposable.
                if (!ReferenceEquals(newProvider, _activeProvider) && _activeProvider is IDisposable old)
                {
                    try { old.Dispose(); }
                    catch { /* swallow disposal errors during refresh */ }
                }

                _activeProvider = newProvider;
            }
        }

        private IProfilerHistoryProvider GetOrCreateProvider()
        {
            lock (_sync)
            {
                if (_disposed) return null;
                if (_activeProvider == null) RefreshProvider();
                return _activeProvider;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;

                if (_activeProvider is IDisposable d)
                {
                    try { d.Dispose(); }
                    catch { /* swallow disposal errors during shutdown */ }
                }
                _activeProvider = null;

                if (_factory is IDisposable f)
                {
                    try { f.Dispose(); }
                    catch { /* swallow disposal errors during shutdown */ }
                }
            }
        }
    }
}