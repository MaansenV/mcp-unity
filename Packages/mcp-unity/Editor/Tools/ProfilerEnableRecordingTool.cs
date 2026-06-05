using Newtonsoft.Json.Linq;
using McpUnity.Profiler;
using McpUnity.Unity;

namespace McpUnity.Tools
{
    public sealed class ProfilerEnableRecordingTool : McpToolBase
    {
        public ProfilerEnableRecordingTool()
        {
            Name = "profiler_enable_recording";
            Description = "Enable or disable Unity Profiler recording";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                parameters ??= new JObject();
                var enabled = parameters["enabled"]?.Value<bool>() ?? true;

                var service = ProfilerHistoryService.Instance;
                var success = service.SetRecordingEnabled(enabled, out var status, out var error);

                var serializer = new ProfilerFrameSerializer();
                var data = serializer.SerializeStatus(status);

                if (success)
                {
                    return serializer.CreateSuccessResponse($"Profiler recording {(enabled ? "enabled" : "disabled")}", data);
                }
                else
                {
                    return serializer.CreateErrorResponse(error, "profiler_recording_failed", data);
                }
            }
            catch (System.Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"Error setting profiler recording: {ex.Message}", "profiler_error");
            }
        }
    }
}