using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor.TestTools.TestRunner.Api;

namespace McpUnity.Services
{
    /// <summary>
    /// Interface for the test runner service
    /// </summary>
    public interface ITestRunnerService
    {
        /// <summary>
        /// Asynchronously retrieves all available tests using the TestRunnerApi.
        /// </summary>
        /// <param name="testModeFilter">Optional test mode filter (EditMode, PlayMode, or empty for all)</param>
        /// <returns>List of test items matching the specified test mode, or all tests if no mode specified</returns>
        Task<List<ITestAdaptor>> GetAllTestsAsync(string testModeFilter = "");

        /// <summary>
        /// Executes tests using the TestRunnerApi and returns the results as a JSON object.
        /// Used internally for synchronous EditMode test execution when needed.
        /// </summary>
        /// <param name="testMode">The test mode to run (EditMode or PlayMode).</param>
        /// <param name="returnOnlyFailures">If true, only failed test results are included in the output.</param>
        /// <param name="returnWithLogs">If true, all logs are included in the output.</param>
        /// <param name="testFilter">A filter string to select specific tests to run.</param>
        /// <returns>Task that resolves with test results when tests are complete</returns>
        Task<JObject> ExecuteTestsAsync(TestMode testMode, bool returnOnlyFailures, bool returnWithLogs, string testFilter);

        /// <summary>
        /// Starts a test job that persists across Domain Reload and WebSocket disconnects.
        /// Returns immediately with a jobId. Works for both EditMode and PlayMode.
        /// </summary>
        /// <param name="testMode">The test mode to run (EditMode or PlayMode).</param>
        /// <param name="returnOnlyFailures">If true, only failed test results are included.</param>
        /// <param name="returnWithLogs">If true, all logs are included.</param>
        /// <param name="testFilter">A filter string to select specific tests.</param>
        /// <returns>JSON object with jobId and status</returns>
        JObject StartTestJob(TestMode testMode, bool returnOnlyFailures, bool returnWithLogs, string testFilter);

        /// <summary>
        /// Gets the status/result of a persisted test job.
        /// </summary>
        /// <param name="jobId">The job ID to query</param>
        /// <returns>JSON object with job status and results if available</returns>
        JObject GetTestJobStatus(string jobId);
    }
}
