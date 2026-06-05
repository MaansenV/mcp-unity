using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using Newtonsoft.Json.Linq;

namespace McpUnity.Services
{
    /// <summary>
    /// Service for accessing Unity Test Runner functionality.
    /// Implements ICallbacks for TestRunnerApi.
    /// 
    /// All test runs are managed as persistent jobs via TestJobStore.
    /// This ensures test results survive Domain Reload and WebSocket disconnects.
    /// </summary>
    public class TestRunnerService : ITestRunnerService, ICallbacks, IDisposable
    {
        private readonly TestRunnerApi _testRunnerApi;
        private readonly TestJobStore _jobStore;

        // Only used for legacy synchronous ExecuteTestsAsync (EditMode only)
        private TaskCompletionSource<JObject> _tcs;
        private bool _returnOnlyFailures;
        private bool _returnWithLogs;
        private List<ITestResultAdaptor> _results;

        // Active job tracking
        private string _activeJobId;
        private TestMode? _activeMode;

        /// <summary>
        /// Constructor
        /// </summary>
        public TestRunnerService()
        {
            _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            _results = new List<ITestResultAdaptor>();
            _jobStore = new TestJobStore();
            _testRunnerApi.RegisterCallbacks(this);

            RestoreActiveJobIfNeeded();
        }

        /// <summary>
        /// After Domain Reload, check if there was an active job that needs to be resumed.
        /// Handles both running PlayMode jobs (restore callbacks) and pending jobs (restart or fail).
        /// </summary>
        private void RestoreActiveJobIfNeeded()
        {
            var activeJob = _jobStore.GetActiveJob();
            if (activeJob != null)
            {
                _activeJobId = activeJob.JobId;
                _activeMode = activeJob.TestMode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode;
                McpLogger.LogInfo($"Restored active test job after reload: {_activeJobId} ({activeJob.TestMode}, status={activeJob.Status})");

                // If job was pending (never started), restart it
                if (activeJob.Status == "pending")
                {
                    McpLogger.LogInfo($"Restarting pending job {_activeJobId} after domain reload");
                    RestartPendingJob(_activeJobId);
                }
                // If job was running and is PlayMode, callbacks will be received by this new instance
                // If job was running and is EditMode, it likely completed before reload (EditMode doesn't trigger reload)
            }
        }

        /// <summary>
        /// Restart a pending job that was scheduled but not executed before domain reload.
        /// </summary>
        private void RestartPendingJob(string jobId)
        {
            var job = _jobStore.Get(jobId);
            if (job == null || job.Status != "pending")
                return;

            // Re-schedule execution with double-defer to let WebSocket response flush
            MainThreadDispatcher.PostAfterUpdatesAsync(2, async () =>
            {
                try
                {
                    await ExecuteJobInternal(jobId);
                }
                catch (Exception ex)
                {
                    McpLogger.LogError($"Test job {jobId} failed with exception: {ex.Message}");
                    _jobStore.Fail(jobId, ex.Message, "job_execution_exception");
                }
            });
        }

        /// <summary>
        /// Clean up TestRunnerApi callbacks.
        /// </summary>
        public void Dispose()
        {
            if (_testRunnerApi != null)
            {
                try
                {
                    _testRunnerApi.UnregisterCallbacks(this);
                }
                catch (Exception ex)
                {
                    McpLogger.LogWarning($"Error unregistering TestRunner callbacks: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Async retrieval of all tests using TestRunnerApi callbacks
        /// </summary>
        public async Task<List<ITestAdaptor>> GetAllTestsAsync(string testModeFilter = "")
        {
            var tests = new List<ITestAdaptor>();
            var tasks = new List<Task<List<ITestAdaptor>>>();

            if (string.IsNullOrEmpty(testModeFilter) || testModeFilter.Equals("EditMode", StringComparison.OrdinalIgnoreCase))
            {
                tasks.Add(RetrieveTestsAsync(TestMode.EditMode));
            }
            if (string.IsNullOrEmpty(testModeFilter) || testModeFilter.Equals("PlayMode", StringComparison.OrdinalIgnoreCase))
            {
                tasks.Add(RetrieveTestsAsync(TestMode.PlayMode));
            }

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                tests.AddRange(result);
            }

            return tests;
        }

        /// <summary>
        /// Starts a persistent test job for both EditMode and PlayMode.
        /// Returns immediately with a jobId. The actual test run is deferred via MainThreadDispatcher.
        /// This prevents WebSocket disconnects during test execution.
        /// </summary>
        public JObject StartTestJob(TestMode testMode, bool returnOnlyFailures, bool returnWithLogs, string testFilter)
        {
            // Check editor readiness
            if (EditorApplication.isCompiling)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Unity is currently compiling scripts. Please wait and try again.",
                    "editor_busy_compiling");
            }
            if (EditorApplication.isUpdating)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Unity is currently updating assets. Please wait and try again.",
                    "editor_busy_updating");
            }

            // Check for existing active job (any mode)
            var existingActive = _jobStore.GetActiveJob();
            if (existingActive != null)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = $"A test job is already active: {existingActive.JobId} ({existingActive.TestMode}, {existingActive.Status})",
                    ["jobId"] = existingActive.JobId,
                    ["status"] = existingActive.Status
                };
            }

            // Create persistent job
            var job = _jobStore.CreatePlayModeJob(testFilter, returnOnlyFailures, returnWithLogs);
            if (job == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Failed to create test job",
                    "job_creation_failed");
            }

            // Override TestMode on the job (CreatePlayModeJob defaults to PlayMode)
            job.TestMode = testMode.ToString();
            _jobStore.Save();

            _activeJobId = job.JobId;
            _activeMode = testMode;

            McpLogger.LogInfo($"Starting test job {job.JobId}: Mode={testMode}, Filter={testFilter ?? "(none)"}");

            // Defer actual execution to let the WebSocket response flush first.
            // Double defer (2 editor updates) ensures the response is sent before Unity potentially
            // triggers assembly reload or other disruptive operations.
            // Uses MainThreadDispatcher (EditorApplication.update-based) instead of delayCall
            // so it works even when Unity Editor is not focused.
            string jobId = job.JobId;
            MainThreadDispatcher.PostAfterUpdatesAsync(2, async () =>
            {
                try
                {
                    await ExecuteJobInternal(jobId);
                }
                catch (Exception ex)
                {
                    McpLogger.LogError($"Test job {jobId} failed with exception: {ex.Message}");
                    _jobStore.Fail(jobId, ex.Message, "job_execution_exception");
                }
            });

            return new JObject
            {
                ["success"] = true,
                ["status"] = "started",
                ["jobId"] = job.JobId,
                ["testMode"] = testMode.ToString(),
                ["message"] = $"Test job started: {job.JobId}. Use get_test_job_status to poll results."
            };
        }

        /// <summary>
        /// Gets the status of a persisted test job.
        /// </summary>
        public JObject GetTestJobStatus(string jobId)
        {
            return _jobStore.GetStatusJson(jobId);
        }

        /// <summary>
        /// Internal method to execute a test job via TestRunnerApi.
        /// Called after delayCall to ensure WebSocket response is flushed.
        /// </summary>
        private async Task ExecuteJobInternal(string jobId)
        {
            var job = _jobStore.Get(jobId);
            if (job == null)
            {
                McpLogger.LogWarning($"Test job {jobId} not found, cannot execute");
                return;
            }

            TestMode testMode = job.TestMode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode;

            var filter = new Filter { testMode = testMode };

            // Build filter from job parameters
            if (!string.IsNullOrEmpty(job.TestFilter))
            {
                var matchingTests = await FindMatchingTestsAsync(testMode, job.TestFilter);

                if (matchingTests.Count > 0)
                {
                    filter.testNames = matchingTests.ToArray();
                    McpLogger.LogInfo($"Job {jobId}: Found {matchingTests.Count} tests matching filter '{job.TestFilter}'");
                }
                else
                {
                    filter.testNames = new[] { job.TestFilter };
                    McpLogger.LogInfo($"Job {jobId}: No tests found matching '{job.TestFilter}', trying exact match");
                }
            }

            _jobStore.MarkRunning(jobId, "Test Run");

            // Pre-execution logging for diagnostics
            McpLogger.LogInfo($"Job {jobId}: Starting TestRunnerApi.Execute. " +
                $"isCompiling={EditorApplication.isCompiling}, " +
                $"isUpdating={EditorApplication.isUpdating}");

            _testRunnerApi.Execute(new ExecutionSettings(filter));
        }

        /// <summary>
        /// Legacy synchronous test execution. Kept for backward compatibility.
        /// Note: This is fragile for long-running tests due to potential WebSocket disconnects.
        /// Prefer StartTestJob + GetTestJobStatus for new code.
        /// </summary>
        public async Task<JObject> ExecuteTestsAsync(TestMode testMode, bool returnOnlyFailures, bool returnWithLogs, string testFilter = "")
        {
            var filter = new Filter { testMode = testMode };

            _tcs = new TaskCompletionSource<JObject>();
            _returnOnlyFailures = returnOnlyFailures;
            _returnWithLogs = returnWithLogs;
            _results = new List<ITestResultAdaptor>();

            testFilter = testFilter?.Trim();

            if (!string.IsNullOrEmpty(testFilter))
            {
                var matchingTests = await FindMatchingTestsAsync(testMode, testFilter);

                if (matchingTests.Count > 0)
                {
                    filter.testNames = matchingTests.ToArray();
                    McpLogger.LogInfo($"Found {matchingTests.Count} tests matching filter '{testFilter}'");
                }
                else
                {
                    filter.testNames = new[] { testFilter };
                    McpLogger.LogInfo($"No tests found matching '{testFilter}', trying exact match");
                }
            }

            _testRunnerApi.Execute(new ExecutionSettings(filter));

            int testTimeout = Math.Max(McpUnitySettings.Instance.RequestTimeoutSeconds, 120);
            return await WaitForCompletionAsync(testTimeout);
        }

        #region ICallbacks Implementation

        public void RunStarted(ITestAdaptor testsToRun)
        {
            // Job-based path
            if (!string.IsNullOrEmpty(_activeJobId) && _activeMode.HasValue)
            {
                _jobStore.MarkRunning(_activeJobId, testsToRun?.Name);
                McpLogger.LogInfo($"Test run started for job {_activeJobId}: {testsToRun?.Name}");
                return;
            }

            // Legacy synchronous path
            if (_tcs == null)
                return;

            _results.Clear();
            McpLogger.LogInfo($"Test run started: {testsToRun?.Name}");
        }

        public void TestStarted(ITestAdaptor test)
        {
            // Optionally implement per-test start logic or logging.
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            // Job-based path
            if (!string.IsNullOrEmpty(_activeJobId) && _activeMode.HasValue)
            {
                var job = _jobStore.Get(_activeJobId);
                if (job == null) return;

                var resultJson = BuildSingleResultJson(result, job.ReturnOnlyFailures, job.ReturnWithLogs);
                if (resultJson != null)
                {
                    _jobStore.AddResult(_activeJobId, resultJson);
                }
                return;
            }

            // Legacy synchronous path
            if (_tcs == null)
                return;

            _results.Add(result);
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            // Job-based path
            if (!string.IsNullOrEmpty(_activeJobId) && _activeMode.HasValue)
            {
                var job = _jobStore.Get(_activeJobId);
                if (job == null) return;

                var summary = BuildResultJsonFromJob(job, result);
                _jobStore.Complete(_activeJobId, summary);

                McpLogger.LogInfo($"Test job {_activeJobId} completed: {result.PassCount}/{result.PassCount + result.FailCount + result.SkipCount} passed");

                _activeJobId = null;
                _activeMode = null;
                return;
            }

            // Legacy synchronous path
            if (_tcs == null)
                return;

            var legacySummary = BuildResultJson(_results, result);
            _tcs.TrySetResult(legacySummary);
            _tcs = null;
        }

        #endregion

        #region Helpers

        private async Task<List<string>> FindMatchingTestsAsync(TestMode testMode, string testFilter)
        {
            var allTests = await RetrieveTestsAsync(testMode);
            var matchingNames = new List<string>();

            foreach (var test in allTests)
            {
                if ((test.FullName?.IndexOf(testFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (test.Name?.IndexOf(testFilter, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    matchingNames.Add(test.FullName);
                }
            }

            return matchingNames;
        }

        private async Task<List<ITestAdaptor>> RetrieveTestsAsync(TestMode mode)
        {
            var tcs = new TaskCompletionSource<List<ITestAdaptor>>();
            var tests = new List<ITestAdaptor>();

            _testRunnerApi.RetrieveTestList(mode, adaptor =>
            {
                CollectTestItems(adaptor, tests);
                tcs.TrySetResult(tests);
            });

            var timeout = TimeSpan.FromSeconds(Math.Max(McpUnitySettings.Instance.RequestTimeoutSeconds, 30));
            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                McpLogger.LogWarning($"Test discovery timed out after {timeout.TotalSeconds}s for mode {mode}");
                return new List<ITestAdaptor>();
            }

            return await tcs.Task;
        }

        private void CollectTestItems(ITestAdaptor testAdaptor, List<ITestAdaptor> tests)
        {
            if (testAdaptor.IsSuite)
            {
                foreach (var child in testAdaptor.Children)
                {
                    CollectTestItems(child, tests);
                }
            }
            else
            {
                tests.Add(testAdaptor);
            }
        }

        private async Task<JObject> WaitForCompletionAsync(int timeoutSeconds)
        {
            var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var winner = await Task.WhenAny(_tcs.Task, delayTask);

            if (winner != _tcs.Task)
            {
                _tcs.TrySetResult(
                    McpUnitySocketHandler.CreateErrorResponse(
                        $"Test run timed out after {timeoutSeconds} seconds",
                        "test_runner_timeout"));
            }
            return await _tcs.Task;
        }

        /// <summary>
        /// Build a single test result as JSON for persistent storage.
        /// </summary>
        private JObject BuildSingleResultJson(ITestResultAdaptor r, bool returnOnlyFailures, bool returnWithLogs)
        {
            if (returnOnlyFailures && !r.ResultState.StartsWith("Failed"))
                return null;

            return new JObject
            {
                ["name"] = r.Name,
                ["fullName"] = r.FullName,
                ["state"] = r.ResultState,
                ["message"] = r.Message,
                ["duration"] = r.Duration,
                ["logs"] = returnWithLogs ? r.Output : null,
                ["stackTrace"] = r.StackTrace
            };
        }

        /// <summary>
        /// Build the final result JSON from a completed job.
        /// </summary>
        private JObject BuildResultJsonFromJob(TestJob job, ITestResultAdaptor result)
        {
            int testCount = result.PassCount + result.SkipCount + result.FailCount;

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"{result.Test.Name} test run completed: {result.PassCount}/{testCount} passed - {result.FailCount}/{testCount} failed - {result.SkipCount}/{testCount} skipped",
                ["resultState"] = result.ResultState,
                ["durationSeconds"] = result.Duration,
                ["testCount"] = job.TestCount,
                ["passCount"] = result.PassCount,
                ["failCount"] = result.FailCount,
                ["skipCount"] = result.SkipCount,
                ["results"] = job.Results ?? new JArray()
            };
        }

        /// <summary>
        /// Legacy result builder for backward compatibility.
        /// </summary>
        private JObject BuildResultJson(List<ITestResultAdaptor> results, ITestResultAdaptor result)
        {
            var arr = new JArray(results
                .Where(r => !r.HasChildren)
                .Where(r => !_returnOnlyFailures || r.ResultState.StartsWith("Failed"))
                .Select(r => new JObject {
                    ["name"]      = r.Name,
                    ["fullName"]  = r.FullName,
                    ["state"]     = r.ResultState,
                    ["message"]   = r.Message,
                    ["duration"]  = r.Duration,
                    ["logs"]      = _returnWithLogs ? r.Output : null,
                    ["stackTrace"] = r.StackTrace
                }));

            int testCount = result.PassCount + result.SkipCount + result.FailCount;
            return new JObject {
                ["success"]           = true,
                ["type"]              = "text",
                ["message"]           = $"{result.Test.Name} test run completed: {result.PassCount}/{testCount} passed - {result.FailCount}/{testCount} failed - {result.SkipCount}/{testCount} skipped",
                ["resultState"]       = result.ResultState,
                ["durationSeconds"]   = result.Duration,
                ["testCount"]         = results.Count,
                ["passCount"]         = result.PassCount,
                ["failCount"]         = result.FailCount,
                ["skipCount"]         = result.SkipCount,
                ["results"]           = arr
            };
        }

        #endregion
    }
}
