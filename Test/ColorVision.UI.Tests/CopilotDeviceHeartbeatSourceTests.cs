using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotDeviceHeartbeatSourceTests
{
    [Fact]
    public void DefaultFalseHeartbeatRemainsUnknownWhenTheExistingOperationalStatusIsOpened()
    {
        WpfTestHost.Invoke(() =>
        {
            var device = new CachedDevice("default", DeviceStatusType.Opened);
            Assert.False(device.IsAlive);
            Assert.Null(device.ObservedIsAlive);

            var item = Assert.Single(CopilotDeviceContextFactory.Capture(device).Items);

            Assert.Contains("Device status: Unknown", item.Content, StringComparison.Ordinal);
            Assert.Contains("Heartbeat status: unknown", item.Content, StringComparison.Ordinal);
            Assert.Contains("IsAlive: unknown", item.Content, StringComparison.Ordinal);
            Assert.Contains("OperationalStatus: Opened", item.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("Offline", item.Content, StringComparison.Ordinal);
            Assert.False(device.IsAlive);
            Assert.Null(device.ObservedIsAlive);
        });
    }

    [Theory]
    [InlineData(true, "Online", "yes", "true")]
    [InlineData(false, "Offline", "no", "false")]
    public void ExplicitBooleanAssignmentsRemainObservationsEvenWhenFalseEqualsTheInitialValue(
        bool alive, string status, string heartbeat, string runtime)
    {
        WpfTestHost.Invoke(() =>
        {
            var device = new CachedDevice("explicit", DeviceStatusType.Unknown);
            Assert.Null(device.ObservedIsAlive);
            int notifications = 0;
            device.PropertyChanged += (_, _) => notifications++;
            device.IsAlive = alive;

            var item = Assert.Single(CopilotDeviceContextFactory.Capture(device).Items);

            Assert.Equal(alive, device.IsAlive);
            Assert.Equal((bool?)alive, device.ObservedIsAlive);
            Assert.Contains($"Device status: {status}", item.Content, StringComparison.Ordinal);
            Assert.Contains($"Heartbeat status: {heartbeat}", item.Content, StringComparison.Ordinal);
            Assert.Contains($"IsAlive: {runtime}", item.Content, StringComparison.Ordinal);
            Assert.Contains("OperationalStatus: Unknown", item.Content, StringComparison.Ordinal);
            Assert.Equal(0, notifications);
        });
    }

    [Fact]
    public void ALastSeenTimestampAloneDoesNotInventAnObservedOnlineOrOfflineBoolean()
    {
        WpfTestHost.Invoke(() =>
        {
            var device = new CachedDevice("timestamp", DeviceStatusType.OffLine);
            var timestamp = new DateTime(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc);
            device.LastAliveTime = timestamp;

            var item = Assert.Single(CopilotDeviceContextFactory.Capture(device).Items);

            Assert.Null(device.ObservedIsAlive);
            Assert.Contains("Device status: Unknown", item.Content, StringComparison.Ordinal);
            Assert.Contains("Heartbeat status: unknown", item.Content, StringComparison.Ordinal);
            Assert.Contains("IsAlive: unknown", item.Content, StringComparison.Ordinal);
            Assert.Contains(timestamp.ToString("O", CultureInfo.InvariantCulture), item.Content, StringComparison.Ordinal);
            Assert.Contains("OperationalStatus: OffLine", item.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("Device status: Offline", item.Content, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void FleetCountsAllKnownAndUnknownObservationsBeforeLimitingItsDisplayedDeviceRows()
    {
        WpfTestHost.Invoke(() =>
        {
            var devices = Enumerable.Range(0, 65).Select(index => new CachedDevice("Device" + index,
                index % 3 == 0 ? DeviceStatusType.Opened : index % 3 == 1 ? DeviceStatusType.Closed : null)).ToArray();
            devices[0].LastAliveTime = new DateTime(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc);
            devices[63].IsAlive = true;
            devices[64].IsAlive = false;

            var item = Assert.Single(CopilotDeviceContextFactory.CaptureFleet(devices)!.Items);

            Assert.Contains("Registered devices: 65", item.Content, StringComparison.Ordinal);
            Assert.Contains("Online: 1", item.Content, StringComparison.Ordinal);
            Assert.Contains("Offline: 1", item.Content, StringComparison.Ordinal);
            Assert.Contains("Unknown: 63", item.Content, StringComparison.Ordinal);
            Assert.Equal("devices 65 · online 1 · offline 1 · unknown 63", item.Summary);
            var rows = item.Content.Split(Environment.NewLine).Where(line => line.StartsWith("- Device", StringComparison.Ordinal)).ToArray();
            Assert.Equal(60, rows.Length);
            Assert.All(rows, line => Assert.Contains(" / Unknown", line, StringComparison.Ordinal));
            Assert.Contains(rows, line => line.Contains(" / state Opened", StringComparison.Ordinal));
            Assert.Contains(rows, line => line.Contains(" / state Closed", StringComparison.Ordinal));
            Assert.DoesNotContain(rows, line => line.Contains(" / Online", StringComparison.Ordinal) || line.Contains(" / Offline", StringComparison.Ordinal));
            Assert.All(devices.Take(63), device => Assert.Null(device.ObservedIsAlive));
            Assert.True(devices[63].ObservedIsAlive);
            Assert.False(devices[64].ObservedIsAlive);
        });
    }

    private sealed class CachedDevice : DeviceService
    {
        private readonly MQTTServiceBase? _cachedService;
        private readonly DeviceServiceConfig _config = new();

        public CachedDevice(string name, DeviceStatusType? operationalStatus)
        {
            Name = name;
            Code = "device-" + name;
            SysResourceModel = new() { Name = name, Code = Code, Type = (int)ServiceTypes.Motor };
            if (operationalStatus.HasValue)
            {
                _cachedService = (MQTTServiceBase)RuntimeHelpers.GetUninitializedObject(typeof(MQTTServiceBase));
                _cachedService.DeviceStatus = operationalStatus.Value;
            }
        }

        public override object GetConfig() => _config;
        public override MQTTServiceBase? GetMQTTService() => _cachedService;
        public override CopilotBusinessContextBundle CaptureCopilotContext() => CopilotDeviceContextFactory.Capture(this);
    }
}
