using Newtonsoft.Json.Linq;
using McpUnity.Profiler;
using McpUnity.Unity;

namespace McpUnity.Tools
{
    public sealed class ProfilerStatusTool : McpToolBase
    {
        public ProfilerStatusTool()
        {
            Name = "profiler_status";
            Description = "Get comprehensive profiler history status including active provider, frame range, and capabilities";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                var service = ProfilerHistoryService.Instance;
                var status = service.GetStatus();
                var serializer = new ProfilerFrameSerializer();

                var data = serializer.SerializeStatus(status);
                return serializer.CreateSuccessResponse("Retrieved profiler history status", data);
            }
            catch (System.Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"Error getting profiler status: {ex.Message}", "profiler_error");
            }
        }
    }
}