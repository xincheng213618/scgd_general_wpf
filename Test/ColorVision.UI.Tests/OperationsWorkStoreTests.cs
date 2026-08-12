using ColorVision.UI.Desktop.Operations;
using System.Collections.Concurrent;
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
                Assert.NotNull(complete.CompletedAt);
                Assert.Contains(store.GetAudit(), item => item.Action == "job.local_cosign");

                OperationsJobSummary summary = OperationsJobSummaryFactory.Create(complete);
                Assert.Equal("service-host-receipt", summary.Evidence.Kind);
                Assert.Equal("success", summary.Evidence.Outcome);
                Assert.Contains(summary.Timeline, item => item.Stage == "mobile_approval" && item.State == "approved");
                Assert.Contains(summary.Timeline, item => item.Stage == "local_cosign" && item.State == "approved");
                Assert.Contains(summary.Timeline, item => item.Stage == "execution" && item.State == "completed");
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void FlowCancellationCompletesAfterMobileApprovalWithoutLocalCoSign()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                OperationsJob job = store.CreateJob("ops.flow.cancel", "phone-1", "Cancel current flow",
                    JsonSerializer.SerializeToElement(new { }), "correlation-1");

                Assert.Equal("awaiting_mobile_approval", job.Status);
                Assert.Null(store.LocalCoSign(job.JobId, true));

                OperationsJob approved = Assert.IsType<OperationsJob>(store.DecideJob(
                    job.JobId, "phone-1", true, "confirmed", "correlation-2"));
                Assert.Equal("approved_mobile", approved.Status);
                Assert.Null(approved.LocalCoSignedAt);

                OperationsJob completed = Assert.IsType<OperationsJob>(store.CompleteJob(
                    job.JobId, true, "flow_cancel:flow_cancel_requested"));
                OperationsJobSummary summary = OperationsJobSummaryFactory.Create(completed);
                Assert.Equal("completed", completed.Status);
                Assert.False(summary.RequiresLocalCoSign);
                Assert.Equal("flow-cancel-request-receipt", summary.Evidence.Kind);
                Assert.Contains(summary.Timeline, item => item.Stage == "local_cosign" && item.State == "not_required");
                Assert.Contains(summary.Timeline, item => item.Stage == "execution" && item.State == "completed");
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
                OperationsSupportSession session = firstStore.RequestSupport(
                    "phone", "guided", "help", 15, "request-1");
                Assert.NotNull(firstStore.LocalConsentSupport(session.SessionId, true));
                OperationsSupportMessage first = firstStore.AddSupportMessage(
                    session.SessionId, "web-relay", "Check the cable", "web-task-message-1");

                OperationsWorkStore restartedStore = new(path);
                OperationsSupportMessage repeated = restartedStore.AddSupportMessage(
                    session.SessionId, "web-relay", "Check the cable", "web-task-message-1");

                Assert.Equal(first.MessageId, repeated.MessageId);
                Assert.Single(restartedStore.GetSupportMessages());
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void SupportMessagesRequireActiveOwnedSessionAndRemainDeviceIsolated()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                OperationsSupportSession session = store.RequestSupport(
                    "phone-a", "guided", "private reason", 15, "request-1");

                InvalidOperationException inactive = Assert.Throws<InvalidOperationException>(() =>
                    store.AddDeviceSupportMessage(session.SessionId, "phone-a", "hello", "message-1"));
                Assert.Equal("support_session_not_active", inactive.Message);
                Assert.NotNull(store.LocalConsentSupport(session.SessionId, true));

                OperationsSupportMessage sent = store.AddDeviceSupportMessage(
                    session.SessionId, "phone-a", "hello", "message-2");
                Assert.Equal("device", sent.Source);
                Assert.Single(store.GetSupportMessagesForDevice("phone-a"));
                Assert.Empty(store.GetSupportMessagesForDevice("phone-b"));
                Assert.Null(store.GetSupportSessionForDevice(session.SessionId, "phone-b"));

                InvalidOperationException foreign = Assert.Throws<InvalidOperationException>(() =>
                    store.AddDeviceSupportMessage(session.SessionId, "phone-b", "hello", "message-3"));
                Assert.Equal("support_session_not_found", foreign.Message);
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void ConcurrentSupportRequestsReuseOneLiveSession()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                ConcurrentBag<string> sessionIds = [];

                Parallel.For(0, 32, index =>
                {
                    OperationsSupportSession session = store.RequestSupport(
                        "phone", "guided", "help", 15, $"request-{index}");
                    sessionIds.Add(session.SessionId);
                });

                Assert.Single(sessionIds.Distinct());
                Assert.Single(store.GetSupportSessionsForDevice("phone"));
                Assert.Single(store.GetAudit().Where(item => item.Action == "support.request"));
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void ThrottledAuditCoalescesEquivalentHighFrequencyReads()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);

                Assert.True(store.RecordAuditThrottled(
                    "phone", "device", "monitor.read", "live-monitor", "completed", "corr-1",
                    TimeSpan.FromMinutes(5)));
                Assert.False(store.RecordAuditThrottled(
                    "phone", "device", "monitor.read", "live-monitor", "completed", "corr-2",
                    TimeSpan.FromMinutes(5)));
                Assert.True(store.RecordAuditThrottled(
                    "phone", "device", "monitor.read", "live-monitor", "failed", "corr-3",
                    TimeSpan.FromMinutes(5)));

                Assert.Equal(2, store.GetAudit().Count(item => item.Action == "monitor.read"));
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
