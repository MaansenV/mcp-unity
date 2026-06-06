using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using McpUnity.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace McpUnity.Services
{
    /// <summary>
    /// Persistent storage for test jobs.
    /// Jobs survive Domain Reload via JSON file in Library/ folder.
    /// </summary>
    public class TestJobStore
    {
        private const string StoreFolder = "Library/McpUnity";
        private const string StoreFileName = "TestJobs.json";

        private List<TestJob> _jobs;
        private bool _isLoaded;

        /// <summary>
        /// Path to the JSON store file
        /// </summary>
        private readonly string _storePath;

        /// <summary>
        /// Path to temporary backup file for atomic writes
        /// </summary>
        private string TempPath => _storePath + ".tmp";

        /// <summary>
        /// Creates a new TestJobStore and loads existing jobs
        /// </summary>
        public TestJobStore(string storePath = null)
        {
            _storePath = storePath ?? Path.Combine(
                Application.dataPath,
                "..",
                StoreFolder,
                StoreFileName);

            _jobs = new List<TestJob>();
            Load();
        }

        /// <summary>
        /// Creates a new test job (for both EditMode and PlayMode)
        /// </summary>
        public TestJob CreatePlayModeJob(string testFilter, bool returnOnlyFailures, bool returnWithLogs)
        {
            // Check if there's already an active job (any mode)
            var active = GetActiveJob();
            if (active != null)
            {
                McpLogger.LogWarning($"Cannot create new test job: active job {active.JobId} ({active.TestMode}) still exists");
                return null;
            }

            var job = new TestJob
            {
                TestMode = "PlayMode", // Default, will be overridden by caller
                TestFilter = testFilter ?? "",
                ReturnOnlyFailures = returnOnlyFailures,
                ReturnWithLogs = returnWithLogs
            };

            _jobs.Add(job);
            Save();

            McpLogger.LogInfo($"Created test job: {job.JobId}");
            return job;
        }

        /// <summary>
        /// Gets a job by ID
        /// </summary>
        public TestJob Get(string jobId)
        {
            return _jobs.FirstOrDefault(j => j.JobId == jobId);
        }

        /// <summary>
        /// Gets the currently active PlayMode job (pending or running)
        /// </summary>
        public TestJob GetActivePlayModeJob()
        {
            return _jobs.FirstOrDefault(j => 
                j.TestMode == "PlayMode" && 
                j.IsActive);
        }

        /// <summary>
        /// Gets the currently active job of any mode (pending or running)
        /// </summary>
        public TestJob GetActiveJob()
        {
            return _jobs.FirstOrDefault(j => j.IsActive);
        }

        /// <summary>
        /// Marks a job as running
        /// </summary>
        public void MarkRunning(string jobId, string runName)
        {
            var job = Get(jobId);
            if (job == null) return;

            job.Status = "running";
            job.RunName = runName;
            job.StartedAtUtc = DateTime.UtcNow.ToString("o");
            job.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            Save();

            McpLogger.LogInfo($"Job {jobId} marked as running");
        }

        /// <summary>
        /// Adds a test result to a job
        /// </summary>
        public void AddResult(string jobId, JObject result)
        {
            var job = Get(jobId);
            if (job == null) return;

            if (job.Results == null)
                job.Results = new JArray();

            job.Results.Add(result);
            job.UpdatedAtUtc = DateTime.UtcNow.ToString("o");

            // Update counters
            string state = result["state"]?.ToString() ?? "";
            job.TestCount++;
            
            if (state.StartsWith("Passed"))
                job.PassCount++;
            else if (state.StartsWith("Failed"))
                job.FailCount++;
            else if (state.StartsWith("Skipped"))
                job.SkipCount++;

            // Save periodically (every 10 results) to avoid excessive I/O
            if (job.TestCount % 10 == 0)
                Save();
        }

        /// <summary>
        /// Marks a job as completed
        /// </summary>
        public void Complete(string jobId, JObject finalResult)
        {
            var job = Get(jobId);
            if (job == null) return;

            job.Status = "completed";
            job.FinalResult = finalResult;
            job.CompletedAtUtc = DateTime.UtcNow.ToString("o");
            job.UpdatedAtUtc = DateTime.UtcNow.ToString("o");

            // Extract counts from final result if available
            if (finalResult != null)
            {
                job.TestCount = finalResult["testCount"]?.ToObject<int>() ?? job.TestCount;
                job.PassCount = finalResult["passCount"]?.ToObject<int>() ?? job.PassCount;
                job.FailCount = finalResult["failCount"]?.ToObject<int>() ?? job.FailCount;
                job.SkipCount = finalResult["skipCount"]?.ToObject<int>() ?? job.SkipCount;
                job.Message = finalResult["message"]?.ToString();
            }

            Save();
            McpLogger.LogInfo($"Job {jobId} completed: {job.PassCount}/{job.TestCount} passed");
        }

        /// <summary>
        /// Marks a job as failed
        /// </summary>
        public void Fail(string jobId, string message, string errorType)
        {
            var job = Get(jobId);
            if (job == null) return;

            job.Status = "failed";
            job.ErrorMessage = message;
            job.ErrorType = errorType;
            job.CompletedAtUtc = DateTime.UtcNow.ToString("o");
            job.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            Save();

            McpLogger.LogWarning($"Job {jobId} failed: {message}");
        }

        /// <summary>
        /// Gets job status as JSON
        /// </summary>
        public JObject GetStatusJson(string jobId)
        {
            var job = Get(jobId);
            if (job == null)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["status"] = "notFound",
                    ["message"] = $"Test job not found: {jobId}"
                };
            }

            var result = new JObject
            {
                ["success"] = true,
                ["jobId"] = job.JobId,
                ["status"] = job.Status,
                ["testMode"] = job.TestMode,
                ["message"] = job.Message ?? $"Job status: {job.Status}",
                ["updatedAtUtc"] = job.UpdatedAtUtc
            };

            if (job.IsActive)
            {
                // Include partial results for running jobs
                result["partial"] = new JObject
                {
                    ["testCount"] = job.TestCount,
                    ["passCount"] = job.PassCount,
                    ["failCount"] = job.FailCount,
                    ["skipCount"] = job.SkipCount,
                    ["results"] = job.Results ?? new JArray()
                };
            }
            else if (job.IsTerminal)
            {
                // Include final result for completed jobs
                result["result"] = job.FinalResult;
                result["completedAtUtc"] = job.CompletedAtUtc;

                if (job.Status == "failed")
                {
                    result["errorMessage"] = job.ErrorMessage;
                    result["errorType"] = job.ErrorType;
                }
            }

            return result;
        }

        /// <summary>
        /// Saves all jobs to disk (atomic write)
        /// </summary>
        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(_storePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var wrapper = new JObject
                {
                    ["version"] = 1,
                    ["jobs"] = JArray.FromObject(_jobs)
                };

                string json = wrapper.ToString(Formatting.Indented);

                // Atomic write: temp file → copy → delete temp
                File.WriteAllText(TempPath, json);
                
                if (File.Exists(_storePath))
                    File.Delete(_storePath);
                    
                File.Move(TempPath, _storePath);
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Failed to save test jobs: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads jobs from disk
        /// </summary>
        public void Load()
        {
            if (_isLoaded) return;

            try
            {
                if (!File.Exists(_storePath))
                {
                    _isLoaded = true;
                    return;
                }

                string json = File.ReadAllText(_storePath);
                var wrapper = JObject.Parse(json);
                var jobsArray = wrapper["jobs"] as JArray;

                if (jobsArray != null)
                {
                    _jobs = jobsArray.ToObject<List<TestJob>>() ?? new List<TestJob>();
                }

                _isLoaded = true;
                McpLogger.LogInfo($"Loaded {_jobs.Count} test jobs from store");
            }
            catch (JsonException ex)
            {
                McpLogger.LogWarning($"Corrupted test jobs file, starting fresh: {ex.Message}");
                _jobs = new List<TestJob>();
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Failed to load test jobs: {ex.Message}");
                _jobs = new List<TestJob>();
                _isLoaded = true;
            }
        }

        /// <summary>
        /// Cleans up old completed jobs (older than specified days)
        /// </summary>
        public void CleanupOldJobs(int maxAgeDays = 7)
        {
            var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
            var toRemove = _jobs.Where(j => 
                j.IsTerminal && 
                !string.IsNullOrEmpty(j.CompletedAtUtc) &&
                DateTime.Parse(j.CompletedAtUtc) < cutoff).ToList();

            foreach (var job in toRemove)
            {
                _jobs.Remove(job);
            }

            if (toRemove.Count > 0)
            {
                Save();
                McpLogger.LogInfo($"Cleaned up {toRemove.Count} old test jobs");
            }
        }
    }
}
