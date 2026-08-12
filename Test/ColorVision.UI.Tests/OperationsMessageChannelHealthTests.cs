using ColorVision.UI.Desktop.Operations;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsMessageChannelHealthTests
    {
        [Fact]
        public void FactorySeparatesConnectionAndSubscriptionReadiness()
        {
            DateTimeOffset observedAt = new(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
            OperationsMessageChannelHealthSnapshot disconnected = OperationsMessageChannelHealthSnapshotFactory.Create(
                new OperationsMessageChannelObservation(true, false, 4, 0), observedAt);
            OperationsMessageChannelHealthSnapshot degraded = OperationsMessageChannelHealthSnapshotFactory.Create(
                new OperationsMessageChannelObservation(true, true, 4, 3), observedAt);
            OperationsMessageChannelHealthSnapshot connected = OperationsMessageChannelHealthSnapshotFactory.Create(
                new OperationsMessageChannelObservation(true, true, 4, 4), observedAt);

            Assert.Equal(OperationsMessageChannelStates.Disconnected, disconnected.State);
            Assert.True(disconnected.AttentionRequired);
            Assert.Equal(OperationsMessageChannelStates.Degraded, degraded.State);
            Assert.False(degraded.SubscriptionReady);
            Assert.Equal(OperationsMessageChannelStates.Connected, connected.State);
            Assert.True(connected.SubscriptionReady);
            Assert.False(connected.AttentionRequired);
        }

        [Fact]
        public void SnapshotContainsNoEndpointTopicPayloadOrCredentialFields()
        {
            OperationsMessageChannelHealthSnapshot snapshot = OperationsMessageChannelHealthSnapshotFactory.Create(
                new OperationsMessageChannelObservation(true, true, 2, 2, DateTimeOffset.UtcNow));

            string json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain("\"host\":", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"port\":", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"endpoint\":", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"topic\":", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"payload\":", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"username\":", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"password\":", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
