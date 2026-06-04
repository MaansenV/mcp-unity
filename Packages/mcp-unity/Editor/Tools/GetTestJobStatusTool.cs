using McpUnity.Unity;
using McpUnity.Services;
using McpUnity.Utils;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving status and results of persisted test jobs.
    /// Used to poll test job status after WebSocket disconnect/reconnect.
    /// </summary>
    public class GetTestJobStatusTool : McpToolBase
    {
        private readonly ITestRunnerService _testRunnerService;

        public GetTestJobStatusTool(ITestRunnerService testRunnerService)
        {
            Name = "get_test_job_status";
            Description = "Gets status and result for a persisted Unity test job. Use this to poll test job status after the Unity editor reconnects following a domain reload or play mode transition.";
            IsAsync = false;
            _testRunnerService = testRunnerService;
        }

        public override JObject Execute(JObject parameters)
        {
            string jobId = parameters?["jobId"]?.ToObject<string>();

            if (string.IsNullOrWhiteSpace(jobId))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Missing required parameter: jobId",
                    "missing_parameter");
            }

            McpLogger.LogInfo($"Getting status for test job: {jobId}");
            return _testRunnerService.GetTestJobStatus(jobId);
        }
    }
}
