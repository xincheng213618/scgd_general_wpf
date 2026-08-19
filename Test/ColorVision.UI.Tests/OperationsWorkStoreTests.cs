using ColorVision.UI.Desktop.Operations;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsWorkStoreTests
    {
        [Fact]
        public void FixedMqttRestartExecutesAfterMobileDecisionWithoutLocalCoSign()
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
                Assert.Equal("approved_mobile", decided.Status);
                Assert.Null(decided.LocalCoSignedAt);

                OperationsJob executing = Assert.IsType<OperationsJob>(store.BeginExecution(job.JobId));
                Assert.Equal("executing", executing.Status);
                Assert.Null(store.BeginExecution(job.JobId));
                OperationsJob complete = Assert.IsType<OperationsJob>(store.CompleteJob(job.JobId, true, "servicehost:req-1"));
                Assert.Equal("completed", complete.Status);
                Assert.NotNull(complete.CompletedAt);
                Assert.DoesNotContain(store.GetAudit(), item => item.Action == "job.local_cosign");
                Assert.Contains(store.GetAudit(), item => item.Action == "job.execution.start");

                OperationsJobSummary summary = OperationsJobSummaryFactory.Create(complete);
                Assert.False(summary.RequiresLocalCoSign);
                Assert.Equal("service-host-receipt", summary.Evidence.Kind);
                Assert.Equal("success", summary.Evidence.Outcome);
                Assert.Contains(summary.Timeline, item => item.Stage == "mobile_approval" && item.State == "approved");
                Assert.Contains(summary.Timeline, item => item.Stage == "local_cosign" && item.State == "not_required");
                Assert.Contains(summary.Timeline, item => item.Stage == "execution" && item.State == "completed");
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void FixedMqttRestartRejectsMissingDifferentOrAdditionalInput()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                JsonElement[] invalidInputs =
                [
                    JsonSerializer.SerializeToElement(new { }),
                    JsonSerializer.SerializeToElement(new { serviceId = "other-service" }),
                    JsonSerializer.SerializeToElement(new { serviceId = "mosquitto", command = "restart" }),
                ];

                foreach (JsonElement input in invalidInputs)
                {
                    InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                        store.CreateJob("ops.service.restart", "phone-1", "Restart broker", input, "correlation"));
                    Assert.Equal("mqtt_restart_input_not_allowed", error.Message);
                }
                Assert.Empty(store.GetJobs());
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

                OperationsJob executing = Assert.IsType<OperationsJob>(store.BeginExecution(job.JobId));
                Assert.Equal("executing", executing.Status);
                Assert.Contains(OperationsJobSummaryFactory.Create(executing).Timeline,
                    item => item.Stage == "execution" && item.State == "in_progress");
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

        [Theory]
        [InlineData("ops.diagnostics.bundle.create")]
        [InlineData("ops.window.snapshot.capture")]
        [InlineData("ops.application.restart")]
        [InlineData("ops.messaging.reconnect")]
        public void PairedPhoneDirectJobsExecuteAfterMobileDecisionWithoutLocalCoSign(string capabilityId)
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                OperationsJob job = store.CreateJob(capabilityId, "phone-1", "Remote evidence",
                    JsonSerializer.SerializeToElement(new { }), "correlation-1");

                OperationsJob approved = Assert.IsType<OperationsJob>(store.DecideJob(
                    job.JobId, "phone-1", true, "confirmed", "correlation-2"));
                Assert.Equal("approved_mobile", approved.Status);
                Assert.False(OperationsJobSummaryFactory.Create(approved).RequiresLocalCoSign);
                Assert.NotNull(store.BeginExecution(job.JobId));
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Theory]
        [InlineData("ops.diagnostics.bundle.create")]
        [InlineData("ops.window.snapshot.capture")]
        [InlineData("ops.flow.cancel")]
        [InlineData("ops.application.restart")]
        [InlineData("ops.messaging.reconnect")]
        public void PairedPhoneParameterlessJobsRejectRemoteInput(string capabilityId)
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                    store.CreateJob(capabilityId, "phone-1", "Remote evidence",
                        JsonSerializer.SerializeToElement(new { command = "remote" }), "correlation"));

                Assert.Equal("job_input_not_allowed", error.Message);
                Assert.Empty(store.GetJobs());
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void ApplicationRestartRejectsInputFromEverySource()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                    store.CreateJob("ops.application.restart", "web-relay", "restart",
                        JsonSerializer.SerializeToElement(new { path = "remote.exe" }), "task"));

                Assert.Equal("job_input_not_allowed", error.Message);
                Assert.Empty(store.GetJobs());
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
                OperationsJob approved = Assert.IsType<OperationsJob>(store.DecideJob(
                    first.JobId, "phone-1", true, "confirmed", "decision"));
                Assert.Equal("awaiting_local_cosign", approved.Status);
                Assert.True(OperationsJobSummaryFactory.Create(approved).RequiresLocalCoSign);
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void RelayIntentIdempotencyPreservesTheOriginalFailureOutcomeAfterRestart()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore firstStore = new(path);
                firstStore.RecordAudit(
                    "phone-1", "device", "relay.intent.execute",
                    "ops.flow.cancel", "failed", "cancel-request-1");

                OperationsWorkStore reloadedStore = new(path);
                Assert.Equal("failed", reloadedStore.GetProcessedRelayIntentOutcome(
                    "phone-1", "cancel-request-1"));
                Assert.Null(reloadedStore.GetProcessedRelayIntentOutcome(
                    "phone-1", "different-request"));
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void RelayRestartCorrelationAndFinalReceiptMarkerSurviveProcessRestart()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore firstStore = new(path);
                OperationsJob created = firstStore.CreateJob(
                    "ops.application.restart",
                    "phone-1",
                    "Restart current application",
                    JsonSerializer.SerializeToElement(new { }),
                    "restart-idempotency-1",
                    "restart-task-1",
                    "restart-idempotency-1");
                firstStore.RecordAudit(
                    "operations-relay", "system", "relay.restart.receipt",
                    "restart-task-1", "completed", "restart-idempotency-1");

                OperationsWorkStore reloadedStore = new(path);
                OperationsJob reloaded = Assert.Single(reloadedStore.GetJobs());
                Assert.Equal(created.JobId, reloaded.JobId);
                Assert.Equal("restart-task-1", reloaded.SourceTaskId);
                Assert.Equal("restart-idempotency-1", reloaded.SourceIdempotencyKey);
                Assert.True(reloadedStore.HasSentRelayRestartReceipt(
                    "restart-task-1", "restart-idempotency-1", "completed"));
                Assert.False(reloadedStore.HasSentRelayRestartReceipt(
                    "restart-task-1", "restart-idempotency-1", "failed"));
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void RelayMqttRestartSourceIdentityAndTerminalResultSurviveProcessRestart()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore firstStore = new(path);
                OperationsJob job = firstStore.CreateJob(
                    "ops.service.restart",
                    "phone-1",
                    "Restart fixed MQTT service",
                    JsonSerializer.SerializeToElement(new { serviceId = "mosquitto" }),
                    "mqtt-restart-idempotency",
                    "mqtt-restart-task",
                    "mqtt-restart-idempotency");
                job = Assert.IsType<OperationsJob>(firstStore.DecideJob(
                    job.JobId, "phone-1", true, "confirmed", "mqtt-restart-idempotency"));
                job = Assert.IsType<OperationsJob>(firstStore.BeginExecution(job.JobId));
                job = Assert.IsType<OperationsJob>(firstStore.CompleteJob(
                    job.JobId, true, "servicehost:request-1"));

                OperationsWorkStore reloadedStore = new(path);
                OperationsJob reloaded = Assert.Single(reloadedStore.GetJobs());
                Assert.Equal("completed", reloaded.Status);
                Assert.Equal("mqtt-restart-task", reloaded.SourceTaskId);
                Assert.Equal("mqtt-restart-idempotency", reloaded.SourceIdempotencyKey);
                Assert.Equal("servicehost:request-1", reloaded.ResultEvidenceId);

                OperationsJob deduplicated = reloadedStore.CreateJob(
                    "ops.service.restart",
                    "phone-1",
                    "Restart fixed MQTT service",
                    JsonSerializer.SerializeToElement(new { serviceId = "mosquitto" }),
                    "mqtt-restart-idempotency",
                    "mqtt-restart-task",
                    "mqtt-restart-idempotency");
                Assert.Equal(reloaded.JobId, deduplicated.JobId);
                Assert.Equal("completed", deduplicated.Status);

                OperationsJob replayedWithAnotherRelayTaskId = reloadedStore.CreateJob(
                    "ops.service.restart",
                    "phone-1",
                    "Restart fixed MQTT service",
                    JsonSerializer.SerializeToElement(new { serviceId = "mosquitto" }),
                    "mqtt-restart-idempotency",
                    "mqtt-restart-task-rewrapped",
                    "mqtt-restart-idempotency");
                Assert.Equal(reloaded.JobId, replayedWithAnotherRelayTaskId.JobId);
                Assert.Single(reloadedStore.GetJobs());
            }
            finally
            {
                DeletePath(path);
            }
        }

        [Fact]
        public void RelayRestartReceiptMarkerIncludesTheSourceIdempotencyKey()
        {
            string path = NewPath();
            try
            {
                OperationsWorkStore store = new(path);
                store.RecordAudit(
                    "operations-relay", "system", "relay.restart.receipt",
                    "shared-task", "completed", "first-key");

                Assert.True(store.HasSentRelayRestartReceipt(
                    "shared-task", "first-key", "completed"));
                Assert.False(store.HasSentRelayRestartReceipt(
                    "shared-task", "second-key", "completed"));
                Assert.False(store.HasSentRelayRestartReceipt(
                    "shared-task", "first-key", "failed"));
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
