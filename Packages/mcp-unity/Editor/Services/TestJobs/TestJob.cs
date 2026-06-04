using System;
using Newtonsoft.Json.Linq;

namespace McpUnity.Services
{
    /// <summary>
    /// Data transfer object for a persisted test job.
    /// Survives Domain Reload via JSON serialization.
    /// </summary>
    [Serializable]
    public class TestJob
    {
        /// <summary>
        /// Unique identifier for this job (GUID)
        /// </summary>
        public string JobId;

        /// <summary>
        /// Current status: pending, running, completed, failed, timedOut, canceled
        /// </summary>
        public string Status;

        /// <summary>
        /// Test mode: EditMode or PlayMode
        /// </summary>
        public string TestMode;

        /// <summary>
        /// Optional filter string for test selection
        /// </summary>
        public string TestFilter;

        /// <summary>
        /// If true, only failed test results are included
        /// </summary>
        public bool ReturnOnlyFailures;

        /// <summary>
        /// If true, all logs are included in the output
        /// </summary>
        public bool ReturnWithLogs;

        /// <summary>
        /// UTC timestamp when the job was created
        /// </summary>
        public string CreatedAtUtc;

        /// <summary>
        /// UTC timestamp when the job started running
        /// </summary>
        public string StartedAtUtc;

        /// <summary>
        /// UTC timestamp when the job was last updated
        /// </summary>
        public string UpdatedAtUtc;

        /// <summary>
        /// UTC timestamp when the job completed
        /// </summary>
        public string CompletedAtUtc;

        /// <summary>
        /// Total number of tests found
        /// </summary>
        public int TestCount;

        /// <summary>
        /// Number of passing tests
        /// </summary>
        public int PassCount;

        /// <summary>
        /// Number of failing tests
        /// </summary>
        public int FailCount;

        /// <summary>
        /// Number of skipped tests
        /// </summary>
        public int SkipCount;

        /// <summary>
        /// Human-readable message about the job status
        /// </summary>
        public string Message;

        /// <summary>
        /// Error message if the job failed
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// Error type/code if the job failed
        /// </summary>
        public string ErrorType;

        /// <summary>
        /// Individual test results (serialized as JSON array)
        /// </summary>
        public JArray Results;

        /// <summary>
        /// Final aggregated result (serialized as JSON object)
        /// </summary>
        public JObject FinalResult;

        /// <summary>
        /// Name of the test run (from TestRunner callback)
        /// </summary>
        public string RunName;

        /// <summary>
        /// Creates a new TestJob with default values
        /// </summary>
        public TestJob()
        {
            JobId = Guid.NewGuid().ToString("N");
            Status = "pending";
            CreatedAtUtc = DateTime.UtcNow.ToString("o");
            Results = new JArray();
        }

        /// <summary>
        /// Checks if this job is in a terminal state
        /// </summary>
        public bool IsTerminal => Status == "completed" || 
                                  Status == "failed" || 
                                  Status == "timedOut" || 
                                  Status == "canceled";

        /// <summary>
        /// Checks if this job is currently active (pending or running)
        /// </summary>
        public bool IsActive => Status == "pending" || Status == "running";
    }
}
