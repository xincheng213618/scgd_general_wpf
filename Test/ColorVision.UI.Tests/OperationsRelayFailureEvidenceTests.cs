using System.IO;
using System.Text.Json;
using ColorVision.UI.Desktop.Operations;

namespace ColorVision.UI.Tests;

public sealed class OperationsRelayFailureEvidenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void HandlerProjectsOnlyTheExactBoundedAggregateAndRepeatReadsStaySideEffectFree()
    {
        string root = NewRoot();
        try
        {
            DateTimeOffset observedAt = new(2026, 8, 13, 9, 0, 0, TimeSpan.Zero);
            OperationsFailureEvidenceSnapshot snapshot = new()
            {
                Available = true,
                EventLogAvailable = true,
                DumpFolderAvailable = true,
                EventScanLimited = true,
                DumpScanLimited = false,
                HasEvidence = true,
                WindowDays = 7,
                FailureEventCount = 2,
                CrashCount = 1,
                HangCount = 0,
                ManagedRuntimeFailureCount = 1,
                WindowsErrorReportCount = 0,
                DumpCount = 1,
                LatestEventAt = observedAt.AddMinutes(-2),
                LatestDumpAt = observedAt.AddMinutes(-1),
                LatestEvidenceAt = observedAt.AddMinutes(-1),
                WindowStartedAt = observedAt.AddDays(-7),
                ObservedAt = observedAt,
                PrivacyNotice = @"secret C:\private\failure.dmp user@example.com",
            };
            RecordingProvider provider = new(snapshot);
            OperationsWorkStore store = new(Path.Combine(root, "work.json"));
            OperationsRelayFailureEvidenceHandler handler = new(provider, store);
            OperationsRelayVerifiedTask task = CreateTask();

            OperationsRelayFailureEvidenceResult first = handler.Handle(task);
            OperationsRelayFailureEvidenceResult second = handler.Handle(task);

            Assert.Equal("completed", first.Status);
            Assert.Equal("completed", second.Status);
            Assert.Equal(2, provider.CaptureCount);
            Assert.Empty(store.GetJobs());
            Assert.Equal(2, store.GetAudit().Count(item =>
                item.ActorId == "device-1"
                && item.ActorType == "device"
                && item.Action == "diagnostics.failure-evidence.read"
                && item.TargetId == "failure-evidence"
                && item.Outcome == "completed"
                && item.CorrelationId == "failure-read-1"));

            string json = JsonSerializer.Serialize(first.Evidence, JsonOptions);
            using JsonDocument document = JsonDocument.Parse(json);
            string[] propertyNames = document.RootElement.EnumerateObject()
                .Select(item => item.Name).Order(StringComparer.Ordinal).ToArray();
            string[] expected =
            [
                "crashCount",
                "dumpCount",
                "dumpFolderAvailable",
                "dumpScanLimited",
                "eventLogAvailable",
                "eventScanLimited",
                "failureEventCount",
                "hangCount",
                "hasEvidence",
                "kind",
                "latestDumpAt",
                "latestEventAt",
                "latestEvidenceAt",
                "managedRuntimeFailureCount",
                "observedAt",
                "windowDays",
                "windowStartedAt",
                "windowsErrorReportCount",
            ];
            Assert.Equal(expected.Order(StringComparer.Ordinal), propertyNames);
            Assert.Equal("failure-evidence-v1", document.RootElement.GetProperty("kind").GetString());
            Assert.Equal(2, document.RootElement.GetProperty("failureEventCount").GetInt32());
            Assert.Equal(1, document.RootElement.GetProperty("dumpCount").GetInt32());
            Assert.DoesNotContain("privacyNotice", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("failure.dmp", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("user@example.com", json, StringComparison.OrdinalIgnoreCase);
            Assert.False(document.RootElement.TryGetProperty("available", out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableOrThrowingProviderReturnsTheExactNonLeakingFailure(
        bool throws)
    {
        string root = NewRoot();
        try
        {
            IOperationsFailureEvidenceProvider provider = throws
                ? new ThrowingProvider()
                : new RecordingProvider(OperationsFailureEvidenceSnapshot.CreateUnavailable());
            OperationsWorkStore store = new(Path.Combine(root, "work.json"));
            OperationsRelayFailureEvidenceResult result =
                new OperationsRelayFailureEvidenceHandler(provider, store).Handle(CreateTask());

            Assert.Equal("failed", result.Status);
            string json = JsonSerializer.Serialize(result.Evidence, JsonOptions);
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.Equal(2, document.RootElement.EnumerateObject().Count());
            Assert.Equal("failure-evidence-error-v1",
                document.RootElement.GetProperty("kind").GetString());
            Assert.Equal("failure_evidence_unavailable",
                document.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("private-provider-exception", json, StringComparison.Ordinal);
            Assert.Empty(store.GetJobs());
            OperationsAuditEntry audit = Assert.Single(store.GetAudit());
            Assert.Equal("failed", audit.Outcome);
            Assert.Equal("failure-read-1", audit.CorrelationId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InconsistentProviderSnapshotFailsClosed()
    {
        string root = NewRoot();
        try
        {
            DateTimeOffset observedAt = DateTimeOffset.UtcNow;
            OperationsFailureEvidenceSnapshot inconsistent = new()
            {
                Available = true,
                EventLogAvailable = true,
                HasEvidence = true,
                WindowDays = 7,
                WindowStartedAt = observedAt.AddDays(-7),
                ObservedAt = observedAt,
            };
            OperationsWorkStore store = new(Path.Combine(root, "work.json"));

            OperationsRelayFailureEvidenceResult result =
                new OperationsRelayFailureEvidenceHandler(
                    new RecordingProvider(inconsistent), store).Handle(CreateTask());

            Assert.Equal("failed", result.Status);
            OperationsRelayFailureEvidenceError error =
                Assert.IsType<OperationsRelayFailureEvidenceError>(result.Evidence);
            Assert.Equal("failure_evidence_unavailable", error.Code);
            Assert.Empty(store.GetJobs());

            OperationsFailureEvidenceSnapshot categoryWithoutTotal = new()
            {
                Available = true,
                EventLogAvailable = true,
                HasEvidence = false,
                WindowDays = 7,
                CrashCount = 1,
                WindowStartedAt = observedAt.AddDays(-7),
                ObservedAt = observedAt,
            };
            OperationsRelayFailureEvidenceResult categoryResult =
                new OperationsRelayFailureEvidenceHandler(
                    new RecordingProvider(categoryWithoutTotal), store).Handle(CreateTask());
            Assert.Equal("failed", categoryResult.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IndependentlyCappedAggregateCountsRemainValid()
    {
        string root = NewRoot();
        try
        {
            DateTimeOffset observedAt = DateTimeOffset.UtcNow;
            OperationsFailureEvidenceSnapshot capped = new()
            {
                Available = true,
                EventLogAvailable = true,
                HasEvidence = true,
                WindowDays = 7,
                FailureEventCount = 999,
                CrashCount = 999,
                HangCount = 999,
                ManagedRuntimeFailureCount = 999,
                WindowsErrorReportCount = 999,
                LatestEventAt = observedAt.AddMinutes(-1),
                LatestEvidenceAt = observedAt.AddMinutes(-1),
                WindowStartedAt = observedAt.AddDays(-7),
                ObservedAt = observedAt,
            };
            OperationsWorkStore store = new(Path.Combine(root, "work.json"));

            OperationsRelayFailureEvidenceResult result =
                new OperationsRelayFailureEvidenceHandler(
                    new RecordingProvider(capped), store).Handle(CreateTask());

            Assert.Equal("completed", result.Status);
            OperationsRelayFailureEvidence evidence =
                Assert.IsType<OperationsRelayFailureEvidence>(result.Evidence);
            Assert.Equal(999, evidence.FailureEventCount);
            Assert.Equal(999, evidence.CrashCount);
            Assert.Equal(999, evidence.HangCount);
            Assert.Equal(999, evidence.ManagedRuntimeFailureCount);
            Assert.Equal(999, evidence.WindowsErrorReportCount);
            Assert.Empty(store.GetJobs());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RelayAcceptsTheConfiguredFailureEvidenceProviderBeforeStart()
    {
        string root = NewRoot();
        try
        {
            RecordingProvider provider = new(OperationsFailureEvidenceSnapshot.CreateUnavailable());
            using OperationsRelayClientService relay = new(
                new OperationsServerIdentity(Path.Combine(root, "identity")),
                new OperationsDeviceRegistry(Path.Combine(root, "devices.json")),
                new OperationsWorkStore(Path.Combine(root, "work.json")));

            relay.ConfigureFailureEvidenceProvider(provider);
            var field = typeof(OperationsRelayClientService).GetField(
                "_failureEvidenceProvider",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            Assert.Same(provider, field.GetValue(relay));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static OperationsRelayVerifiedTask CreateTask() => new()
    {
        TaskId = "task-1",
        CapabilityId = "ops.diagnostics.failures.read",
        IdempotencyKey = "failure-read-1",
        Device = new OperationsPairedDevice { DeviceId = "device-1" },
        Payload = JsonSerializer.SerializeToElement(new { }),
    };

    private static string NewRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cv-relay-failure-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class RecordingProvider(
        OperationsFailureEvidenceSnapshot snapshot) : IOperationsFailureEvidenceProvider
    {
        public int CaptureCount { get; private set; }

        public OperationsFailureEvidenceSnapshot Capture()
        {
            CaptureCount++;
            return snapshot;
        }
    }

    private sealed class ThrowingProvider : IOperationsFailureEvidenceProvider
    {
        public OperationsFailureEvidenceSnapshot Capture() =>
            throw new InvalidOperationException("private-provider-exception");
    }
}
