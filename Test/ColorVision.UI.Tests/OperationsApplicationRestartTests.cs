using ColorVision.UI.Desktop.Operations;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsApplicationRestartTests
    {
        [Fact]
        public void FreshHandoffCompletesExecutingRestartJob()
        {
            string directory = NewDirectory();
            try
            {
                OperationsWorkStore store = new(Path.Combine(directory, "work.json"));
                OperationsJob job = CreateExecutingJob(store);
                string handoffPath = Path.Combine(directory, "restart.json");
                OperationsApplicationRestartHandoff handoff = new(handoffPath);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                handoff.Prepare(job.JobId, now.AddSeconds(-10));

                OperationsJob completed = Assert.IsType<OperationsJob>(
                    handoff.CompletePending(store, job.JobId, now));

                Assert.Equal("completed", completed.Status);
                Assert.Equal("application_restart:completed", completed.ResultEvidenceId);
                Assert.Equal("application-restart-receipt",
                    OperationsJobSummaryFactory.Create(completed).Evidence.Kind);
                Assert.False(File.Exists(handoffPath));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Fact]
        public void StaleHandoffFailsExecutingRestartJob()
        {
            string directory = NewDirectory();
            try
            {
                OperationsWorkStore store = new(Path.Combine(directory, "work.json"));
                OperationsJob job = CreateExecutingJob(store);
                string handoffPath = Path.Combine(directory, "restart.json");
                OperationsApplicationRestartHandoff handoff = new(handoffPath);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                handoff.Prepare(job.JobId, now.AddMinutes(-6));

                OperationsJob failed = Assert.IsType<OperationsJob>(
                    handoff.CompletePending(store, job.JobId, now));

                Assert.Equal("failed", failed.Status);
                Assert.Equal("application_restart:handoff_expired", failed.ResultEvidenceId);
                Assert.False(File.Exists(handoffPath));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Fact]
        public void StartupWithoutMatchingInternalJobTokenFailsPendingHandoff()
        {
            string directory = NewDirectory();
            try
            {
                OperationsWorkStore store = new(Path.Combine(directory, "work.json"));
                OperationsJob job = CreateExecutingJob(store);
                string handoffPath = Path.Combine(directory, "restart.json");
                OperationsApplicationRestartHandoff handoff = new(handoffPath);
                handoff.Prepare(job.JobId);

                OperationsJob failed = Assert.IsType<OperationsJob>(
                    handoff.CompletePending(store, expectedJobId: null));

                Assert.Equal("failed", failed.Status);
                Assert.Equal("application_restart:handoff_mismatch", failed.ResultEvidenceId);
                Assert.False(File.Exists(handoffPath));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Fact]
        public void InternalRestartArgumentsAreRemovedBeforeApplicationParsing()
        {
            string jobId = Guid.NewGuid().ToString("N");

            string[] applicationArguments = ColorVision.OperationsApplicationRestartController
                .WaitForEarlierProcessAndRemoveHandoffArguments(
                ["-r", "--wait-for-process", int.MaxValue.ToString(),
                    "--operations-restart-job", jobId, "sample.cv"]);

            Assert.Equal(["-r", "sample.cv"], applicationArguments);
            Assert.Equal(jobId, ColorVision.OperationsApplicationRestartController.RestartJobId);
        }

        [Fact]
        public void FailureRecoveryArgumentIsInternalAndRegistrationTargetsCurrentApplicationOnly()
        {
            string[] applicationArguments = WindowsApplicationRestartRegistration
                .CaptureAndRemoveRecoveryArguments(
                    ["-r", WindowsApplicationRestartRegistration.RecoveryRestartArgument, "sample.cv"]);

            Assert.Equal(["-r", "sample.cv"], applicationArguments);
            Assert.True(WindowsApplicationRestartRegistration.RestartedAfterFailure);
            Assert.True(WindowsApplicationRestartRegistration.TryRegister());
            try
            {
                StringBuilder commandLine = new(1024);
                uint commandLineLength = checked((uint)commandLine.Capacity);
                int result = GetApplicationRestartSettings(
                    Process.GetCurrentProcess().Handle,
                    commandLine,
                    ref commandLineLength,
                    out uint flags);

                Assert.True(result >= 0, $"GetApplicationRestartSettings failed with HRESULT 0x{result:X8}.");
                Assert.Equal(WindowsApplicationRestartRegistration.RecoveryRestartArgument, commandLine.ToString());
                Assert.Equal(0xCu, flags);
                OperationsApplicationRecoveryStatus status =
                    WindowsApplicationRestartRegistration.CaptureStatus();
                Assert.True(status.Supported);
                Assert.True(status.Registered);
            }
            finally
            {
                Assert.True(WindowsApplicationRestartRegistration.TryUnregisterForCleanExit());
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetApplicationRestartSettings(
            IntPtr process,
            StringBuilder commandLine,
            ref uint commandLineLength,
            out uint flags);

        private static OperationsJob CreateExecutingJob(OperationsWorkStore store)
        {
            OperationsJob job = store.CreateJob(
                "ops.application.restart", "phone-1", "restart",
                JsonSerializer.SerializeToElement(new { }), "request");
            Assert.NotNull(store.DecideJob(job.JobId, "phone-1", true, "confirmed", "decision"));
            return Assert.IsType<OperationsJob>(store.BeginExecution(job.JobId));
        }

        private static string NewDirectory() =>
            Path.Combine(Path.GetTempPath(), "ColorVision.Tests", Guid.NewGuid().ToString("N"));

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
