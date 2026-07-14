using ColorVision.UI.Desktop.Operations;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsWorkStoreTests
    {
        [Fact]
        public void PrivilegedJobCannotSkipMobileDecisionOrLocalCoSign()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                OperationsJob job = store.CreateJob("ops.service.restart", "phone-1", "Restart broker",
                    JsonSerializer.SerializeToElement(new { serviceId = "mosquitto" }), "correlation-1");

                Assert.Equal("awaiting_mobile_approval", job.Status);
                Assert.Null(store.LocalCoSign(job.JobId, true));

                OperationsJob decided = Assert.IsType<OperationsJob>(store.DecideJob(
                    job.JobId, "phone-1", true, "credential verified", "correlation-2"));
                Assert.Equal("awaiting_local_cosign", decided.Status);

                OperationsJob local = Assert.IsType<OperationsJob>(store.LocalCoSign(job.JobId, true));
                Assert.Equal("approved_local", local.Status);
                OperationsJob complete = Assert.IsType<OperationsJob>(store.CompleteJob(job.JobId, true, "servicehost:req-1"));
                Assert.Equal("completed", complete.Status);
                Assert.Contains(store.GetAudit(), item => item.Action == "job.local_cosign");
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void WebRelayTaskIsIdempotentAndStillNeedsHumanApproval()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                JsonElement input = JsonSerializer.SerializeToElement(new { reason = "support" });
                OperationsJob first = store.CreateJob("ops.diagnostics.bundle.create", "web-relay", "support", input, "web-task-1");
                OperationsJob second = store.CreateJob("ops.diagnostics.bundle.create", "web-relay", "support", input, "web-task-1");

                Assert.Equal(first.JobId, second.JobId);
                Assert.Equal("awaiting_mobile_approval", second.Status);
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void DeploymentReceiptAndSupportRequestAreBoundedAndAudited()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                OperationsDeploymentReceipt receipt = store.AddDeploymentReceipt(
                    "phone", "release-1", "1.2.3", "verified", string.Empty, "corr-1");
                OperationsSupportSession support = store.RequestSupport(
                    "phone", "diagnostics", "help", 999, "corr-2");

                Assert.Equal("verified", receipt.Status);
                Assert.InRange((support.ExpiresAt - support.CreatedAt).TotalMinutes, 29.9, 30.1);
                Assert.Equal("awaiting_local_consent", support.Status);
                Assert.True(store.GetAudit().Count >= 2);
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void WebSupportMessageRemainsIdempotentAfterRestart()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore firstStore = new(path);
                OperationsSupportMessage first = firstStore.AddSupportMessage(
                    "session-1", "web-relay", "Check the cable", "web-task-message-1");

                OperationsWorkStore restartedStore = new(path);
                OperationsSupportMessage repeated = restartedStore.AddSupportMessage(
                    "session-1", "web-relay", "Check the cable", "web-task-message-1");

                Assert.Equal(first.MessageId, repeated.MessageId);
                Assert.Single(restartedStore.GetSupportMessages());
            }
            finally
            {
                DeletePath(path);
            }
        }

        private static string NewPath() => Path.Combine(Path.GetTempPath(), "ColorVision.Tests", Guid.NewGuid().ToString("N"), "work.json");

        private static void DeletePath(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
