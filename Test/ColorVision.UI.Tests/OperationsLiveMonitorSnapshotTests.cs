using ColorVision.UI.Desktop.Operations;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsLiveMonitorSnapshotTests
    {
        [Fact]
        public void FactoryReturnsOnlyAggregateAlertCountsAndSafeRuntimeFields()
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
                        Source = "application",
                        Summary = "private log body",
                        OccurredAt = capturedAt.AddMinutes(-2),
                    },
                    new OperationsAlert
                    {
                        AlertId = "private-alert-id-2",
                        Severity = "error",
                        Source = "service",
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
                    new OperationsMessageChannelObservation(true, true, 5, 5), capturedAt));

            Assert.Equal(2, snapshot.Alerts.Count);
            Assert.Equal(1, snapshot.Alerts.WarningCount);
            Assert.Equal(1, snapshot.Alerts.ErrorCount);
            Assert.Equal(capturedAt.AddMinutes(-1), snapshot.Alerts.LatestOccurredAt);
            Assert.Equal(10, snapshot.SuggestedRefreshSeconds);
            Assert.Equal(2, snapshot.Devices.TotalCount);
            Assert.Equal(1, snapshot.Devices.AttentionCount);
            Assert.Equal(1, snapshot.Devices.OfflineCount);
            Assert.Equal(OperationsMessageChannelStates.Connected, snapshot.MessageChannel.State);
            Assert.True(snapshot.MessageChannel.SubscriptionReady);

            string json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain("private-alert-id", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private log body", json, StringComparison.Ordinal);
            Assert.DoesNotContain("flowName", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("processId", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("deviceId", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
