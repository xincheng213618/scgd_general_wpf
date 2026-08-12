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
                new OperationsDeviceHealthObservation(OperationsDeviceCategories.Camera, true),
                new OperationsDeviceHealthObservation(OperationsDeviceCategories.Camera, false),
                new OperationsDeviceHealthObservation(OperationsDeviceCategories.Algorithm, true),
                new OperationsDeviceHealthObservation("private-device-name", false),
            ], observedAt);

            Assert.True(snapshot.Available);
            Assert.True(snapshot.HasConfiguredDevices);
            Assert.False(snapshot.AllOnline);
            Assert.Equal(4, snapshot.TotalCount);
            Assert.Equal(2, snapshot.OnlineCount);
            Assert.Equal(2, snapshot.OfflineCount);
            Assert.Equal(observedAt, snapshot.ObservedAt);
            Assert.Contains(snapshot.Categories, item =>
                item.Category == OperationsDeviceCategories.Camera
                && item.TotalCount == 2 && item.OnlineCount == 1 && item.OfflineCount == 1);
            Assert.Contains(snapshot.Categories, item =>
                item.Category == OperationsDeviceCategories.Other
                && item.TotalCount == 1 && item.OfflineCount == 1);

            string json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain("private-device-name", json, StringComparison.Ordinal);
            Assert.DoesNotContain("deviceId", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lastAliveTime", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EmptyRegistryIsAvailableButNotReportedAsAllOnline()
        {
            OperationsDeviceHealthSnapshot snapshot = OperationsDeviceHealthSnapshotFactory.Create([]);

            Assert.True(snapshot.Available);
            Assert.False(snapshot.HasConfiguredDevices);
            Assert.False(snapshot.AllOnline);
            Assert.Empty(snapshot.Categories);
        }
    }
}
