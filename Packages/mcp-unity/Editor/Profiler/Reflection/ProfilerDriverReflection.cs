using System;
using System.Collections.Generic;
using System.Reflection;

namespace McpUnity.Profiler
{
    public sealed class ProfilerDriverReflection : IProfilerDriverReflection
    {
        private Type _driverType;
        private PropertyInfo _firstFrameIndexProp;
        private PropertyInfo _lastFrameIndexProp;
        private PropertyInfo _currentFrameIndexProp;
        private PropertyInfo _selectedFrameIndexProp;
        private PropertyInfo _enabledProp;
        private MethodInfo _setRecordingEnabledMethod;
        private MethodInfo _getFrameTimeMsMethod;

        public bool IsAvailable { get; private set; }
        public string AvailabilityReason { get; private set; }
        public ReflectionDiscoveryResult Discovery { get; private set; }

        public ProfilerDriverReflection()
        {
            Discover();
        }

        private void Discover()
        {
            var properties = new Dictionary<string, bool>();
            var methods = new Dictionary<string, bool>();
            var warnings = new List<string>();
            var missing = new List<string>();

            _driverType = FindProfilerDriverType();

            if (_driverType == null)
            {
                IsAvailable = false;
                AvailabilityReason = "UnityEditorInternal.ProfilerDriver type not found";
                Discovery = new ReflectionDiscoveryResult
                {
                    ProfilerDriverTypeFound = false,
                    Properties = properties,
                    Methods = methods,
                    MissingRequiredMembers = missing,
                    Warnings = warnings
                };
                return;
            }

            _firstFrameIndexProp = FindProperty(_driverType, "firstFrameIndex", "FirstFrameIndex");
            properties["firstFrameIndex"] = _firstFrameIndexProp != null;
            if (_firstFrameIndexProp == null) missing.Add("firstFrameIndex");

            _lastFrameIndexProp = FindProperty(_driverType, "lastFrameIndex", "LastFrameIndex");
            properties["lastFrameIndex"] = _lastFrameIndexProp != null;
            if (_lastFrameIndexProp == null) missing.Add("lastFrameIndex");

            _currentFrameIndexProp = FindProperty(_driverType, "currentFrameIndex", "CurrentFrameIndex");
            properties["currentFrameIndex"] = _currentFrameIndexProp != null;
            if (_currentFrameIndexProp == null) missing.Add("currentFrameIndex");

            _selectedFrameIndexProp = FindProperty(_driverType, "selectedFrameIndex", "SelectedFrameIndex");
            properties["selectedFrameIndex"] = _selectedFrameIndexProp != null;
            if (_selectedFrameIndexProp == null) warnings.Add("selectedFrameIndex not available");

            _enabledProp = FindProperty(_driverType, "enabled", "isRecording", "IsRecording");
            properties["enabled"] = _enabledProp != null;
            if (_enabledProp == null) missing.Add("enabled");

            _setRecordingEnabledMethod = FindMethod(_driverType, "SetRecordingEnabled", "set_enabled");
            methods["SetRecordingEnabled"] = _setRecordingEnabledMethod != null;

            _getFrameTimeMsMethod = FindMethod(_driverType, "GetFrameTimeMs", "GetFrameTime");
            methods["GetFrameTimeMs"] = _getFrameTimeMsMethod != null;

            IsAvailable = _firstFrameIndexProp != null && _lastFrameIndexProp != null && _enabledProp != null;
            AvailabilityReason = IsAvailable ? "Available" : "Required members missing";

            Discovery = new ReflectionDiscoveryResult
            {
                ProfilerDriverTypeFound = true,
                ProfilerDriverTypeName = _driverType.FullName,
                Properties = properties,
                Methods = methods,
                MissingRequiredMembers = missing,
                Warnings = warnings
            };
        }

        private Type FindProfilerDriverType()
        {
            var typeNames = new[]
            {
                "UnityEditorInternal.ProfilerDriver, UnityEditor",
                "UnityEditorInternal.ProfilerDriver, UnityEditor.CoreModule",
                "UnityEditorInternal.ProfilerDriver"
            };

            foreach (var typeName in typeNames)
            {
                var type = Type.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        private PropertyInfo FindProperty(Type type, params string[] names)
        {
            foreach (var name in names)
            {
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (prop != null) return prop;
            }
            return null;
        }

        private MethodInfo FindMethod(Type type, params string[] names)
        {
            foreach (var name in names)
            {
                var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (method != null) return method;
            }
            return null;
        }

        public bool TryGetFirstFrameIndex(out int frameIndex, out string error)
        {
            frameIndex = 0;
            error = null;
            if (_firstFrameIndexProp == null) { error = "firstFrameIndex property not available"; return false; }
            try { frameIndex = Convert.ToInt32(_firstFrameIndexProp.GetValue(null)); return true; }
            catch (TargetInvocationException ex) { error = ex.InnerException?.Message ?? ex.Message; return false; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryGetLastFrameIndex(out int frameIndex, out string error)
        {
            frameIndex = 0;
            error = null;
            if (_lastFrameIndexProp == null) { error = "lastFrameIndex property not available"; return false; }
            try { frameIndex = Convert.ToInt32(_lastFrameIndexProp.GetValue(null)); return true; }
            catch (TargetInvocationException ex) { error = ex.InnerException?.Message ?? ex.Message; return false; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryGetCurrentFrameIndex(out int frameIndex, out string error)
        {
            frameIndex = 0;
            error = null;
            if (_currentFrameIndexProp == null) { error = "currentFrameIndex property not available"; return false; }
            try { frameIndex = Convert.ToInt32(_currentFrameIndexProp.GetValue(null)); return true; }
            catch (TargetInvocationException ex) { error = ex.InnerException?.Message ?? ex.Message; return false; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryGetSelectedFrameIndex(out int frameIndex, out string error)
        {
            frameIndex = -1;
            error = null;
            if (_selectedFrameIndexProp == null) { error = "selectedFrameIndex property not available"; return false; }
            try { frameIndex = Convert.ToInt32(_selectedFrameIndexProp.GetValue(null)); return true; }
            catch (TargetInvocationException ex) { error = ex.InnerException?.Message ?? ex.Message; return false; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryGetRecordingEnabled(out bool enabled, out string error)
        {
            enabled = false;
            error = null;
            if (_enabledProp == null) { error = "enabled property not available"; return false; }
            try { enabled = Convert.ToBoolean(_enabledProp.GetValue(null)); return true; }
            catch (TargetInvocationException ex) { error = ex.InnerException?.Message ?? ex.Message; return false; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TrySetRecordingEnabled(bool enabled, out string error)
        {
            error = null;
            if (_setRecordingEnabledMethod != null)
            {
                try { _setRecordingEnabledMethod.Invoke(null, new object[] { enabled }); return true; }
                catch (TargetInvocationException ex) { error = ex.InnerException?.Message ?? ex.Message; return false; }
                catch (Exception ex) { error = ex.Message; return false; }
            }
            if (_enabledProp != null && _enabledProp.CanWrite)
            {
                try { _enabledProp.SetValue(null, enabled); return true; }
                catch (TargetInvocationException ex) { error = ex.InnerException?.Message ?? ex.Message; return false; }
                catch (Exception ex) { error = ex.Message; return false; }
            }
            error = "No setter for recording enabled";
            return false;
        }

        public bool TryGetFrameTimeMs(int frameIndex, out double frameTimeMs, out string error)
        {
            frameTimeMs = 0;
            error = null;
            if (_getFrameTimeMsMethod == null) { error = "GetFrameTimeMs method not available"; return false; }
            try { frameTimeMs = Convert.ToDouble(_getFrameTimeMsMethod.Invoke(null, new object[] { frameIndex })); return true; }
            catch (TargetInvocationException ex) { error = ex.InnerException?.Message ?? ex.Message; return false; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryInvokeMethod<T>(string logicalMethodName, object[] args, out T result, out string error)
        {
            result = default;
            error = null;
            if (_driverType == null) { error = "ProfilerDriver type not available"; return false; }
            var method = FindMethod(_driverType, logicalMethodName);
            if (method == null) { error = $"Method {logicalMethodName} not found"; return false; }
            try { result = (T)method.Invoke(null, args); return true; }
            catch (TargetInvocationException ex) { error = ex.InnerException?.Message ?? ex.Message; return false; }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}