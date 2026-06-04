using System;
using System.Threading.Tasks;
using McpUnity.Unity;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityEditor.TestTools.TestRunner.Api;
using McpUnity.Services;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for running Unity Test Runner tests.
    /// All test runs are started as persistent jobs via StartTestJob.
    /// Use get_test_job_status to poll for results.
    /// </summary>
    public class RunTestsTool : McpToolBase
    {
        private readonly ITestRunnerService _testRunnerService;

        public RunTestsTool(ITestRunnerService testRunnerService)
        {
            Name = "run_tests";
            Description = "Runs tests using Unity's Test Runner. Starts a persistent job and returns immediately with a jobId. Use get_test_job_status to poll for results.";
            IsAsync = true;
            _testRunnerService = testRunnerService;
        }

        /// <summary>
        /// Executes the RunTests tool. Always starts a job and returns jobId immediately.
        /// </summary>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            // Parse parameters
            string testModeStr = parameters?["testMode"]?.ToObject<string>() ?? "EditMode";
            string testFilter = parameters?["testFilter"]?.ToObject<string>();
            bool returnOnlyFailures = parameters?["returnOnlyFailures"]?.ToObject<bool>() ?? false;
            bool returnWithLogs = parameters?["returnWithLogs"]?.ToObject<bool>() ?? false;

            TestMode testMode = TestMode.EditMode;
            if (Enum.TryParse(testModeStr, true, out TestMode parsedMode))
            {
                testMode = parsedMode;
            }

            McpLogger.LogInfo($"RunTestsTool: Starting job. Mode={testMode}, Filter={testFilter ?? "(none)"}");

            // Start a persistent job - returns immediately with jobId
            JObject result = _testRunnerService.StartTestJob(testMode, returnOnlyFailures, returnWithLogs, testFilter);
            tcs.SetResult(result);
        }
    }
}
