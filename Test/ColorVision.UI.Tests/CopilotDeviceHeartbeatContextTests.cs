using ColorVision.UI;
using System.Text.Json;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace ColorVision.UI.Tests;

public sealed class CopilotDeviceHeartbeatContextTests
{
    [Fact]
    public void DefaultRowsRemainUnobservedWhileExplicitBooleanInitializersPreserveTheirMeaning()
    {
        var unobserved = new CopilotDeviceHealthContextSnapshot
        {
            ServiceName = "Unobserved", OperationalStatus = "Opened", LastAliveTime = "2026-10-27T01:00:00Z",
        };
        Assert.False(unobserved.IsAlive);
        Assert.False(unobserved.HeartbeatObservationKnown);
        Assert.True(new CopilotDeviceHealthContextSnapshot { IsAlive = true }.HeartbeatObservationKnown);
        Assert.True(new CopilotDeviceHealthContextSnapshot { IsAlive = false }.HeartbeatObservationKnown);

        var context = CopilotBusinessContextBuilder.BuildDeviceFleetContextItem(new CopilotDeviceFleetContextSnapshot
        {
            TotalDevices = 1, UnknownDevices = 1, Devices = [unobserved],
        });
        Assert.Contains("- Unobserved / Unknown / state Opened / last heartbeat 2026-10-27T01:00:00Z", context.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Unobserved / Offline", context.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitUnknownOverridesInitializerAndJsonPropertyOrderWithoutBreakingLegacyBooleanJson()
    {
        foreach (var value in new[] { true, false })
        {
            var first = new CopilotDeviceHealthContextSnapshot { IsAlive = value, HeartbeatObservationKnown = false };
            var second = new CopilotDeviceHealthContextSnapshot { HeartbeatObservationKnown = false, IsAlive = value };
            Assert.Equal(value, first.IsAlive);
            Assert.Equal(value, second.IsAlive);
            Assert.False(first.HeartbeatObservationKnown);
            Assert.False(second.HeartbeatObservationKnown);
            var literal = value ? "true" : "false";
            foreach (var json in new[]
            {
                "{\"IsAlive\":" + literal + ",\"HeartbeatObservationKnown\":false}",
                "{\"HeartbeatObservationKnown\":false,\"IsAlive\":" + literal + "}",
            })
            {
                foreach (var row in DeserializeBoth(json))
                {
                    Assert.Equal(value, row.IsAlive);
                    Assert.False(row.HeartbeatObservationKnown);
                }
            }
            foreach (var row in DeserializeBoth("{\"IsAlive\":" + literal + "}"))
            {
                Assert.Equal(value, row.IsAlive);
                Assert.True(row.HeartbeatObservationKnown);
            }
        }
        var unknown = new CopilotDeviceHealthContextSnapshot();
        foreach (var json in new[] { JsonSerializer.Serialize(unknown), JsonConvert.SerializeObject(unknown) })
            Assert.All(DeserializeBoth(json), row => Assert.False(row.HeartbeatObservationKnown));
    }

    [Fact]
    public void KnownHeartbeatRowsKeepTheirExistingTextAndSummaryWithoutAnUnknownCounter()
    {
        var context = CopilotBusinessContextBuilder.BuildDeviceFleetContextItem(new CopilotDeviceFleetContextSnapshot
        {
            TotalDevices = 2, OnlineDevices = 1, OfflineDevices = 1,
            Devices =
            [
                new() { ServiceName = "Live", ServiceCode = "CAM-A", ServiceType = "Camera", IsAlive = true },
                new() { ServiceName = "Down", IsAlive = false },
            ],
        });
        Assert.Equal("devices 2 · online 1 · offline 1", context.Summary);
        Assert.Contains("Online: 1", context.Content, StringComparison.Ordinal);
        Assert.Contains("Offline: 1", context.Content, StringComparison.Ordinal);
        Assert.Contains("- Live / code CAM-A / Camera / Online", context.Content, StringComparison.Ordinal);
        Assert.Contains("- Down / Offline", context.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Unknown:", context.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(" / Unknown", context.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void MixedFleetUsesFullSourceCountsEvenWhenTheDetailRowsAreLimitedToSixty()
    {
        var rows = Enumerable.Range(0, 65).Select(index => index < 2
            ? new CopilotDeviceHealthContextSnapshot { ServiceName = "device-" + index, IsAlive = true }
            : index == 2 ? new CopilotDeviceHealthContextSnapshot { ServiceName = "device-" + index, IsAlive = false }
            : new CopilotDeviceHealthContextSnapshot { ServiceName = "device-" + index }).ToArray();
        var context = CopilotBusinessContextBuilder.BuildDeviceFleetContextItem(new CopilotDeviceFleetContextSnapshot
        {
            TotalDevices = 65, OnlineDevices = 2, OfflineDevices = 1, UnknownDevices = 62, Devices = rows,
        });
        Assert.Equal("devices 65 · online 2 · offline 1 · unknown 62", context.Summary);
        Assert.Contains("Registered devices: 65", context.Content, StringComparison.Ordinal);
        Assert.Contains("Online: 2", context.Content, StringComparison.Ordinal);
        Assert.Contains("Offline: 1", context.Content, StringComparison.Ordinal);
        Assert.Contains("Unknown: 62", context.Content, StringComparison.Ordinal);
        Assert.Equal(60, context.Content.Split(Environment.NewLine).Count(line => line.StartsWith("- device-", StringComparison.Ordinal)));
        Assert.Contains("- device-59 / Unknown", context.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("- device-60 /", context.Content, StringComparison.Ordinal);
    }

    private static CopilotDeviceHealthContextSnapshot[] DeserializeBoth(string json) =>
    [
        JsonSerializer.Deserialize<CopilotDeviceHealthContextSnapshot>(json)!,
        JsonConvert.DeserializeObject<CopilotDeviceHealthContextSnapshot>(json)!,
    ];
}
