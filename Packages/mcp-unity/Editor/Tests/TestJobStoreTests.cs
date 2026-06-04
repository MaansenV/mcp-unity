using NUnit.Framework;
using McpUnity.Services;
using Newtonsoft.Json.Linq;
using System.IO;

namespace McpUnity.Tests
{
    /// <summary>
    /// Tests for TestJobStore persistence functionality
    /// </summary>
    public class TestJobStoreTests
    {
        private TestJobStore _store;
        private string _testStorePath;

        [SetUp]
        public void SetUp()
        {
            _store = new TestJobStore();
            _testStorePath = Path.Combine(
                Application.dataPath, 
                "..", 
                "Library", 
                "McpUnity", 
                "TestJobs.json");
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test store file
            if (File.Exists(_testStorePath))
            {
                File.Delete(_testStorePath);
            }
        }

        #region CreateJob Tests

        [Test]
        public void CreatePlayModeJob_CreatesJobWithUniqueId()
        {
            // Act
            var job = _store.CreatePlayModeJob("TestFilter", true, false);

            // Assert
            Assert.IsNotNull(job);
            Assert.IsNotNull(job.JobId);
            Assert.IsTrue(job.JobId.Length > 0);
            Assert.AreEqual("PlayMode", job.TestMode);
            Assert.AreEqual("TestFilter", job.TestFilter);
            Assert.IsTrue(job.ReturnOnlyFailures);
            Assert.IsFalse(job.ReturnWithLogs);
            Assert.AreEqual("pending", job.Status);
        }

        [Test]
        public void CreatePlayModeJob_ReturnsNullWhenActiveJobExists()
        {
            // Arrange - Create first job
            _store.CreatePlayModeJob("", false, false);

            // Act - Try to create second job
            var secondJob = _store.CreatePlayModeJob("", false, false);

            // Assert
            Assert.IsNull(secondJob);
        }

        [Test]
        public void CreatePlayModeJob_GeneratesUniqueJobIds()
        {
            // Arrange - Clean up any existing jobs first
            var existingActive = _store.GetActivePlayModeJob();
            if (existingActive != null)
            {
                _store.Complete(existingActive.JobId, null);
            }

            // Act
            var job1 = _store.CreatePlayModeJob("", false, false);
            _store.Complete(job1.JobId, null);
            
            var job2 = _store.CreatePlayModeJob("", false, false);

            // Assert
            Assert.AreNotEqual(job1.JobId, job2.JobId);
        }

        #endregion

        #region GetJob Tests

        [Test]
        public void Get_ReturnsJobIfExists()
        {
            // Arrange
            var created = _store.CreatePlayModeJob("filter", false, false);

            // Act
            var retrieved = _store.Get(created.JobId);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(created.JobId, retrieved.JobId);
        }

        [Test]
        public void Get_ReturnsNullForNonexistentJob()
        {
            // Act
            var result = _store.Get("nonexistent-id-12345");

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetActivePlayModeJob Tests

        [Test]
        public void GetActivePlayModeJob_ReturnsPendingJob()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("", false, false);

            // Act
            var active = _store.GetActivePlayModeJob();

            // Assert
            Assert.IsNotNull(active);
            Assert.AreEqual(job.JobId, active.JobId);
            Assert.AreEqual("pending", active.Status);
        }

        [Test]
        public void GetActivePlayModeJob_ReturnsRunningJob()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("", false, false);
            _store.MarkRunning(job.JobId, "Test Run");

            // Act
            var active = _store.GetActivePlayModeJob();

            // Assert
            Assert.IsNotNull(active);
            Assert.AreEqual("running", active.Status);
        }

        [Test]
        public void GetActivePlayModeJob_ReturnsNullWhenNoActiveJobs()
        {
            // Act
            var active = _store.GetActivePlayModeJob();

            // Assert
            Assert.IsNull(active);
        }

        [Test]
        public void GetActivePlayModeJob_ReturnsNullWhenJobCompleted()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("", false, false);
            _store.Complete(job.JobId, null);

            // Act
            var active = _store.GetActivePlayModeJob();

            // Assert
            Assert.IsNull(active);
        }

        #endregion

        #region MarkRunning Tests

        [Test]
        public void MarkRunning_UpdatesStatus()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("", false, false);

            // Act
            _store.MarkRunning(job.JobId, "My Test Run");

            // Assert
            var updated = _store.Get(job.JobId);
            Assert.AreEqual("running", updated.Status);
            Assert.AreEqual("My Test Run", updated.RunName);
            Assert.IsNotNull(updated.StartedAtUtc);
        }

        #endregion

        #region AddResult Tests

        [Test]
        public void AddResult_IncrementsCounters()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("", false, false);
            _store.MarkRunning(job.JobId, "run");

            var passedResult = new JObject
            {
                ["name"] = "Test1",
                ["state"] = "Passed",
                ["duration"] = 1.0
            };

            var failedResult = new JObject
            {
                ["name"] = "Test2",
                ["state"] = "Failed",
                ["duration"] = 0.5,
                ["message"] = " assertion failed"
            };

            // Act
            _store.AddResult(job.JobId, passedResult);
            _store.AddResult(job.JobId, failedResult);

            // Assert
            var updated = _store.Get(job.JobId);
            Assert.AreEqual(2, updated.TestCount);
            Assert.AreEqual(1, updated.PassCount);
            Assert.AreEqual(1, updated.FailCount);
            Assert.AreEqual(2, updated.Results.Count);
        }

        #endregion

        #region Complete Tests

        [Test]
        public void Complete_SetsTerminalState()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("", false, false);
            _store.MarkRunning(job.JobId, "run");

            var finalResult = new JObject
            {
                ["success"] = true,
                ["message"] = "Tests completed",
                ["testCount"] = 5,
                ["passCount"] = 4,
                ["failCount"] = 1,
                ["skipCount"] = 0
            };

            // Act
            _store.Complete(job.JobId, finalResult);

            // Assert
            var completed = _store.Get(job.JobId);
            Assert.AreEqual("completed", completed.Status);
            Assert.IsNotNull(completed.CompletedAtUtc);
            Assert.IsNotNull(completed.FinalResult);
            Assert.AreEqual(5, completed.TestCount);
            Assert.AreEqual(4, completed.PassCount);
            Assert.AreEqual(1, completed.FailCount);
        }

        #endregion

        #region Fail Tests

        [Test]
        public void Fail_SetsErrorState()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("", false, false);

            // Act
            _store.Fail(job.JobId, "Connection lost", "connection_error");

            // Assert
            var failed = _store.Get(job.JobId);
            Assert.AreEqual("failed", failed.Status);
            Assert.AreEqual("Connection lost", failed.ErrorMessage);
            Assert.AreEqual("connection_error", failed.ErrorType);
        }

        #endregion

        #region GetStatusJson Tests

        [Test]
        public void GetStatusJson_ReturnsNotFoundForMissingJob()
        {
            // Act
            var json = _store.GetStatusJson("nonexistent");

            // Assert
            Assert.IsFalse(json["success"].ToObject<bool>());
            Assert.AreEqual("notFound", json["status"].ToString());
        }

        [Test]
        public void GetStatusJson_ReturnsRunningStatus()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("", false, false);
            _store.MarkRunning(job.JobId, "run");

            // Act
            var json = _store.GetStatusJson(job.JobId);

            // Assert
            Assert.IsTrue(json["success"].ToObject<bool>());
            Assert.AreEqual("running", json["status"].ToString());
            Assert.IsNotNull(json["partial"]);
        }

        [Test]
        public void GetStatusJson_ReturnsCompletedStatus()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("", false, false);
            _store.MarkRunning(job.JobId, "run");
            _store.Complete(job.JobId, new JObject { ["message"] = "done" });

            // Act
            var json = _store.GetStatusJson(job.JobId);

            // Assert
            Assert.IsTrue(json["success"].ToObject<bool>());
            Assert.AreEqual("completed", json["status"].ToString());
            Assert.IsNotNull(json["result"]);
        }

        #endregion

        #region Persistence Tests

        [Test]
        public void SaveAndLoad_PersistsJobs()
        {
            // Arrange
            var job = _store.CreatePlayModeJob("filter", true, true);
            _store.MarkRunning(job.JobId, "run");

            // Act - Create new store instance to force reload
            var newStore = new TestJobStore();
            var loaded = newStore.Get(job.JobId);

            // Assert
            Assert.IsNotNull(loaded);
            Assert.AreEqual(job.JobId, loaded.JobId);
            Assert.AreEqual("running", loaded.Status);
            Assert.AreEqual("filter", loaded.TestFilter);
        }

        [Test]
        public void Save_HandlesCorruptedFileGracefully()
        {
            // Arrange - Write corrupted JSON
            string directory = Path.GetDirectoryName(_testStorePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            File.WriteAllText(_testStorePath, "not valid json {{{");

            // Act - Should not throw
            var store = new TestJobStore();

            // Assert - Store should be empty but functional
            Assert.AreEqual(0, store.GetActivePlayModeJob() == null ? 0 : 1);
        }

        #endregion

        #region IsTerminal / IsActive Tests

        [Test]
        public void IsTerminal_ReturnsTrueForCompletedJobs()
        {
            var job = new TestJob { Status = "completed" };
            Assert.IsTrue(job.IsTerminal);
        }

        [Test]
        public void IsTerminal_ReturnsTrueForFailedJobs()
        {
            var job = new TestJob { Status = "failed" };
            Assert.IsTrue(job.IsTerminal);
        }

        [Test]
        public void IsTerminal_ReturnsFalseForRunningJobs()
        {
            var job = new TestJob { Status = "running" };
            Assert.IsFalse(job.IsTerminal);
        }

        [Test]
        public void IsActive_ReturnsTrueForPendingJobs()
        {
            var job = new TestJob { Status = "pending" };
            Assert.IsTrue(job.IsActive);
        }

        [Test]
        public void IsActive_ReturnsTrueForRunningJobs()
        {
            var job = new TestJob { Status = "running" };
            Assert.IsTrue(job.IsActive);
        }

        [Test]
        public void IsActive_ReturnsFalseForCompletedJobs()
        {
            var job = new TestJob { Status = "completed" };
            Assert.IsFalse(job.IsActive);
        }

        #endregion
    }
}
