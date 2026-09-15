using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using Newtonsoft.Json;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public class CameraBackendRoutingTests
{
    [Fact]
    public void PreferenceDefaultsToServiceAndRoundTrips()
    {
        Assert.False(JsonConvert.DeserializeObject<DisplayCameraConfig>("{}")!.UseLocalCamera);
        var restored = JsonConvert.DeserializeObject<DisplayCameraConfig>(JsonConvert.SerializeObject(new DisplayCameraConfig { UseLocalCamera = true }));
        Assert.True(restored!.UseLocalCamera);
        var state = new CameraBackendState(false);
        state.ObserveService(DeviceStatusType.Opened);
        Assert.False(state.RoutesLocally);
        Assert.Equal(DeviceStatusType.Opened, state.Status);
    }

    [Theory]
    [InlineData(DeviceStatusType.OffLine)]
    [InlineData(DeviceStatusType.Closed)]
    [InlineData(DeviceStatusType.Opened)]
    [InlineData(DeviceStatusType.Unknown)]
    public void ServiceHeartbeatDoesNotOverwriteLocalOpen(DeviceStatusType heartbeat)
    {
        var state = new CameraBackendState(true);
        state.BeginLocalOpen();
        state.SetLocalStatus(DeviceStatusType.Opened);
        state.ObserveService(heartbeat);
        Assert.Equal(heartbeat, state.ServiceStatus);
        Assert.Equal(DeviceStatusType.Opened, state.Status);
        Assert.True(state.RoutesLocally);
        Assert.Throws<InvalidOperationException>(() => state.BeginServiceCommand("GetData"));
    }

    [Fact]
    public void ServiceLossDoesNotReleaseObservedOwnership()
    {
        var state = new CameraBackendState(false);
        state.ObserveService(DeviceStatusType.Opened);
        state.ObserveService(DeviceStatusType.OffLine);
        Assert.Throws<InvalidOperationException>(() => state.BeginLocalOpen());
        Assert.Throws<InvalidOperationException>(() => state.SetPreference(true));
        state.ObserveService(DeviceStatusType.Closed);
        state.SetPreference(true);
        state.BeginLocalOpen();
        Assert.True(state.LocalOwned);
    }

    [Fact]
    public void PendingCommandsAndVideoBlockModeChangesAndOtherBackend()
    {
        var state = new CameraBackendState(false);
        state.BeginServiceCommand("GetData");
        Assert.Throws<InvalidOperationException>(() => state.BeginLocalOpen());
        Assert.Throws<InvalidOperationException>(() => state.SetPreference(true));
        state.EndServiceCommand();
        state.BeginLocalOpen(video: true);
        Assert.Throws<InvalidOperationException>(() => state.BeginServiceCommand("Open"));
        Assert.Throws<InvalidOperationException>(() => state.BeginLocalOpen());
        Assert.Throws<InvalidOperationException>(() => state.SetPreference(true));
        state.EndVideo();
        state.SetPreference(true);
        state.BeginLocalCommand();
        Assert.Throws<InvalidOperationException>(() => state.SetPreference(false));
        Assert.Throws<InvalidOperationException>(() => state.BeginLocalCommand());
        state.EndLocalCommand();
        state.SetPreference(false);
        Assert.False(state.RoutesLocally);
    }

    [Fact]
    public void ConflictStillAllowsLocalCloseThenReturnToServiceOwner()
    {
        var state = new CameraBackendState(true);
        state.BeginLocalOpen();
        state.SetLocalStatus(DeviceStatusType.Opened);
        state.ObserveService(DeviceStatusType.Opened);
        Assert.Throws<InvalidOperationException>(() => state.BeginLocalCommand());
        state.BeginLocalCommand(closing: true);
        state.SetLocalStatus(DeviceStatusType.Closed);
        state.EndLocalCommand();
        state.SetPreference(false);
        Assert.Equal(DeviceStatusType.Opened, state.Status);
    }

    [Fact]
    public void ExplicitLocalManagerOwnsRoutingUntilClosedEvenWithDefaultPreference()
    {
        var state = new CameraBackendState(false);
        state.BeginLocalOpen();
        state.SetLocalStatus(DeviceStatusType.Opened);
        Assert.True(state.RoutesLocally);
        state.SetLocalStatus(DeviceStatusType.Closed);
        Assert.False(state.RoutesLocally);
    }

    [Fact]
    public void MqttCameraUsesEffectiveStateAndBlocksPublishingBeforeMqttAccess()
    {
        // No application, database, MQTT client or native camera is constructed.
        var device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
        var backend = new CameraBackendState(true);
        typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, backend);
        var mqtt = (MQTTCamera)RuntimeHelpers.GetUninitializedObject(typeof(MQTTCamera));
        mqtt.Device = device;
        backend.BeginLocalOpen();
        backend.SetLocalStatus(DeviceStatusType.Opened);
        mqtt.DeviceStatus = DeviceStatusType.OffLine;
        Assert.Equal(DeviceStatusType.Opened, mqtt.DeviceStatus);
        Assert.Throws<InvalidOperationException>(() => mqtt.PublishAsyncClient(new MsgSend { EventName = "GetData" }));
    }
}
