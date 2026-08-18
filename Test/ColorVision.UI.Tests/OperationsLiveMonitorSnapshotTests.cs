using ColorVision.UI.Desktop.Operations;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsLiveMonitorSnapshotTests
    {
        [Fact]
        public void FactoryReturnsSafeAlertSummaryAndRuntimeFields()
        {
            DateTimeOffset capturedAt = new(2026, 8, 12, 8, 0, 0, TimeSpan.Zero);
            OperationsLiveMonitorSnapshot snapshot = OperationsLiveMonitorSnapshotFactory.Create(
                new OperationsFlowRuntimeStatus
                {
                    Available = true,
                    Phase = "running",
                    IsActive = true,
                    ProgressAvailable = true,
                    ProgressPercent = 45,
                },
                new OperationsRuntimePerformanceSnapshot
                {
                    CapturedAt = capturedAt,
                    CpuPercent = 12.5,
                    WorkingSetMb = 256,
                    MainUi = new OperationsUiResponsivenessSnapshot
                    {
                        Available = true,
                        State = "responsive",
                        LatencyMilliseconds = 18,
                    },
                },
                [
                    new OperationsAlert
                    {
                        AlertId = "private-alert-id",
                        Severity = "warning",
                        Source = "应用",
                        Summary = "private log body",
                        OccurredAt = capturedAt.AddMinutes(-2),
                    },
                    new OperationsAlert
                    {
                        AlertId = "private-alert-id-2",
                        Severity = "error",
                        Source = "服务",
                        Summary = "another private log body",
                        OccurredAt = capturedAt.AddMinutes(-1),
                    },
                ],
                OperationsDeviceHealthSnapshotFactory.Create(
                [
                    new OperationsDeviceHealthObservation(OperationsDeviceCategories.Camera, OperationsDeviceStates.Ready),
                    new OperationsDeviceHealthObservation(
                        OperationsDeviceCategories.Camera,
                        OperationsDeviceStates.Unavailable,
                        OperationsDeviceUnavailableReasons.Offline),
                ], capturedAt),
                capturedAt,
                OperationsMessageChannelHealthSnapshotFactory.Create(
                    new OperationsMessageChannelObservation(true, true, 5, 5), capturedAt),
                new OperationsApplicationRecoveryStatus
                {
                    Supported = true,
                    Registered = true,
                    RestartedAfterFailure = true,
                });

            Assert.Equal(2, snapshot.Alerts.Count);
            Assert.Equal(1, snapshot.Alerts.WarningCount);
            Assert.Equal(1, snapshot.Alerts.ErrorCount);
            Assert.Equal("服务", snapshot.Alerts.PrimarySource);
            Assert.Equal(capturedAt.AddMinutes(-1), snapshot.Alerts.LatestOccurredAt);
            Assert.Equal(10, snapshot.SuggestedRefreshSeconds);
            Assert.Equal(2, snapshot.Devices.TotalCount);
            Assert.Equal(1, snapshot.Devices.AttentionCount);
            Assert.Equal(1, snapshot.Devices.OfflineCount);
            Assert.Equal(OperationsMessageChannelStates.Connected, snapshot.MessageChannel.State);
            Assert.True(snapshot.MessageChannel.SubscriptionReady);
            Assert.True(snapshot.ApplicationRecovery.Supported);
            Assert.True(snapshot.ApplicationRecovery.Registered);
            Assert.True(snapshot.ApplicationRecovery.RestartedAfterFailure);

            string json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain("private-alert-id", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private log body", json, StringComparison.Ordinal);
            Assert.DoesNotContain("flowName", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("processId", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("deviceId", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FactoryRejectsUnrecognizedAlertSourcesFromMonitorSummary()
        {
            OperationsLiveMonitorSnapshot snapshot = OperationsLiveMonitorSnapshotFactory.Create(
                OperationsFlowRuntimeStatus.CreateUnavailable(),
                new OperationsRuntimePerformanceSnapshot(),
                [new OperationsAlert
                {
                    Severity = "critical",
                    Source = "private-plugin-name",
                    Summary = "private body",
                    OccurredAt = DateTimeOffset.UtcNow,
                }],
                OperationsDeviceHealthSnapshot.CreateUnavailable());

            Assert.Equal(string.Empty, snapshot.Alerts.PrimarySource);
            string json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain("private-plugin-name", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private body", json, StringComparison.Ordinal);
        }
    }
}
