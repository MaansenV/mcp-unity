using Newtonsoft.Json.Linq;
using McpUnity.Profiler;
using McpUnity.Unity;

namespace McpUnity.Tools
{
    public sealed class ProfilerGetSelectedFrameTool : McpToolBase
    {
        public ProfilerGetSelectedFrameTool()
        {
            Name = "profiler_get_selected_frame";
            Description = "Get the currently selected frame in the Unity Profiler window";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                var service = ProfilerHistoryService.Instance;
                var serializer = new ProfilerFrameSerializer();

                if (!service.TryGetSelectedFrame(out var frame, out var error))
                {
                    return serializer.CreateErrorResponse(error, "profiler_no_selection");
                }

                var data = serializer.SerializeFrameSummary(frame);
                return serializer.CreateSuccessResponse($"Retrieved selected frame {frame.FrameIndex}", data);
            }
            catch (System.Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"Error getting selected frame: {ex.Message}", "profiler_error");
            }
        }
    }
}