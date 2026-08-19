using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Operations;
using ColorVision.UI.Desktop.Operations;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsDeviceHealthTests
    {
        [Fact]
        public void FactoryReturnsOnlyFixedCategoryCounts()
        {
            DateTimeOffset observedAt = new(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);
            OperationsDeviceHealthSnapshot snapshot = OperationsDeviceHealthSnapshotFactory.Create(
            [
                new OperationsDeviceHealthObservation(OperationsDeviceCategories.Camera, OperationsDeviceStates.Ready),
                new OperationsDeviceHealthObservation(
                    OperationsDeviceCategories.Camera,
                    OperationsDeviceStates.Unavailable,
                    "private-raw-reason"),
                new OperationsDeviceHealthObservation(OperationsDeviceCategories.Algorithm, OperationsDeviceStates.Busy),
                new OperationsDeviceHealthObservation("private-device-name", "private-raw-state"),
            ], observedAt);

            Assert.True(snapshot.Available);
            Assert.True(snapshot.HasConfiguredDevices);
            Assert.False(snapshot.AllHealthy);
            Assert.Equal(4, snapshot.TotalCount);
            Assert.Equal(1, snapshot.ReadyCount);
            Assert.Equal(1, snapshot.BusyCount);
            Assert.Equal(1, snapshot.UnavailableCount);
            Assert.Equal(1, snapshot.UnknownCount);
            Assert.Equal(2, snapshot.AttentionCount);
            Assert.Equal(1, snapshot.UnclassifiedUnavailableCount);
            Assert.Equal(observedAt, snapshot.ObservedAt);
            Assert.Contains(snapshot.Categories, item =>
                item.Category == OperationsDeviceCategories.Camera
                && item.TotalCount == 2 && item.ReadyCount == 1 && item.UnavailableCount == 1
                && item.UnclassifiedUnavailableCount == 1);
            Assert.Contains(snapshot.Categories, item =>
                item.Category == OperationsDeviceCategories.Other
                && item.TotalCount == 1 && item.UnknownCount == 1 && item.AttentionCount == 1);

            string json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain("private-device-name", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private-raw-state", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private-raw-reason", json, StringComparison.Ordinal);
            Assert.DoesNotContain("deviceId", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lastAliveTime", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FactoryReturnsOnlyFixedUnavailableReasonsByCoarseCategory()
        {
            OperationsDeviceHealthSnapshot snapshot = OperationsDeviceHealthSnapshotFactory.Create(
            [
                new OperationsDeviceHealthObservation(
                    OperationsDeviceCategories.Camera,
                    OperationsDeviceStates.Unavailable,
                    OperationsDeviceUnavailableReasons.Offline),
                new OperationsDeviceHealthObservation(
                    OperationsDeviceCategories.Camera,
                    OperationsDeviceStates.Unavailable,
                    OperationsDeviceUnavailableReasons.Uninitialized),
                new OperationsDeviceHealthObservation(
                    OperationsDeviceCategories.Camera,
                    OperationsDeviceStates.Unavailable,
                    OperationsDeviceUnavailableReasons.Unauthorized),
                new OperationsDeviceHealthObservation(
                    OperationsDeviceCategories.Camera,
                    OperationsDeviceStates.Unavailable,
                    "private-raw-reason"),
                new OperationsDeviceHealthObservation(
                    OperationsDeviceCategories.Camera,
                    OperationsDeviceStates.Ready,
                    OperationsDeviceUnavailableReasons.Offline),
            ]);

            Assert.Equal(4, snapshot.UnavailableCount);
            Assert.Equal(1, snapshot.OfflineCount);
            Assert.Equal(1, snapshot.UninitializedCount);
            Assert.Equal(1, snapshot.UnauthorizedCount);
            Assert.Equal(1, snapshot.UnclassifiedUnavailableCount);

            string json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain("private-raw-reason", json, StringComparison.Ordinal);
            JsonElement category = Assert.Single(JsonDocument.Parse(json).RootElement
                .GetProperty("Categories").EnumerateArray());
            Assert.Equal(1, category.GetProperty("OfflineCount").GetInt32());
            Assert.Equal(1, category.GetProperty("UninitializedCount").GetInt32());
            Assert.Equal(1, category.GetProperty("UnauthorizedCount").GetInt32());
            Assert.Equal(1, category.GetProperty("UnclassifiedUnavailableCount").GetInt32());
            Assert.DoesNotContain("private-raw-reason", category.GetRawText(),
                StringComparison.Ordinal);
        }

        [Fact]
        public void EmptyRegistryIsAvailableButNotReportedAsAllHealthy()
        {
            OperationsDeviceHealthSnapshot snapshot = OperationsDeviceHealthSnapshotFactory.Create([]);

            Assert.True(snapshot.Available);
            Assert.False(snapshot.HasConfiguredDevices);
            Assert.False(snapshot.AllHealthy);
            Assert.Empty(snapshot.Categories);
        }

        [Theory]
        [InlineData(DeviceStatusType.Opened, OperationsDeviceStates.Ready)]
        [InlineData(DeviceStatusType.Free, OperationsDeviceStates.Ready)]
        [InlineData(DeviceStatusType.LiveOpened, OperationsDeviceStates.Ready)]
        [InlineData(DeviceStatusType.SP_Continuous_Mode, OperationsDeviceStates.Ready)]
        [InlineData(DeviceStatusType.Busy, OperationsDeviceStates.Busy)]
        [InlineData(DeviceStatusType.Opening, OperationsDeviceStates.Transitioning)]
        [InlineData(DeviceStatusType.Closing, OperationsDeviceStates.Transitioning)]
        [InlineData(DeviceStatusType.Closed, OperationsDeviceStates.Closed)]
        [InlineData(DeviceStatusType.Unauthorized, OperationsDeviceStates.Unavailable)]
        [InlineData(DeviceStatusType.UnInit, OperationsDeviceStates.Unavailable)]
        [InlineData(DeviceStatusType.OffLine, OperationsDeviceStates.Unavailable)]
        [InlineData(DeviceStatusType.Unknown, OperationsDeviceStates.Unknown)]
        public void EngineProviderMapsActualMqttStatus(DeviceStatusType status, string expected)
        {
            Assert.Equal(expected, EngineOperationsDeviceHealthProvider.State(status));
        }

        [Fact]
        public void EngineProviderTreatsMissingMqttStatusAsUnknown()
        {
            Assert.Equal(OperationsDeviceStates.Unknown, EngineOperationsDeviceHealthProvider.State(null));
            Assert.Equal(OperationsDeviceUnavailableReasons.None,
                EngineOperationsDeviceHealthProvider.UnavailableReason(null));
        }

        [Theory]
        [InlineData(DeviceStatusType.OffLine, OperationsDeviceUnavailableReasons.Offline)]
        [InlineData(DeviceStatusType.UnInit, OperationsDeviceUnavailableReasons.Uninitialized)]
        [InlineData(DeviceStatusType.Unauthorized, OperationsDeviceUnavailableReasons.Unauthorized)]
        [InlineData(DeviceStatusType.Opened, OperationsDeviceUnavailableReasons.None)]
        public void EngineProviderMapsOnlyFixedUnavailableReasons(DeviceStatusType status, string expected)
        {
            Assert.Equal(expected, EngineOperationsDeviceHealthProvider.UnavailableReason(status));
        }
    }
}
