using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using Newtonsoft.Json;
using System.ComponentModel;
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

    [Fact]
    public void DisplayPreferenceIsEditableAndNotifiesWithoutOpeningCamera()
    {
        var config = new DisplayCameraConfig();
        var property = TypeDescriptor.GetProperties(config)[nameof(DisplayCameraConfig.UseLocalCamera)]!;
        Assert.True(property.IsBrowsable);
        Assert.False(property.IsReadOnly);
        Assert.Equal("使用本地相机", property.DisplayName);
        Assert.Contains("下次打开", property.Description);
        string? changed = null;
        config.PropertyChanged += (_, e) => changed = e.PropertyName;
        property.SetValue(config, true);
        Assert.Equal(nameof(DisplayCameraConfig.UseLocalCamera), changed);
        var state = new CameraBackendState(config.UseLocalCamera);
        Assert.True(state.OpensLocally);
        Assert.False(state.RoutesLocally);
        Assert.Equal(DeviceStatusType.UnInit, state.Status);
    }

    [Fact]
    public void PreferenceAppliesAfterServiceClosesAndLocalPreferenceChangeKeepsCurrentOwner()
    {
        var state = new CameraBackendState(false);
        state.ObserveService(DeviceStatusType.Opened);
        state.SetPreference(true);
        Assert.False(state.RoutesLocally);
        Assert.False(state.OpensLocally);
        Assert.Equal(DeviceStatusType.Opened, state.Status);
        foreach (string command in new[] { "GetData", "GetAutoExpTime", "Close" })
        {
            state.BeginServiceCommand(command);
            state.EndServiceCommand();
        }
        state.ObserveService(DeviceStatusType.Closed);
        Assert.True(state.OpensLocally);
        Assert.False(state.RoutesLocally);
        state.BeginLocalOpen();
        state.SetLocalStatus(DeviceStatusType.Opened);
        state.SetPreference(false);
        Assert.True(state.RoutesLocally);
        Assert.True(state.OpensLocally);
        Assert.Equal(DeviceStatusType.Opened, state.Status);
        state.BeginLocalCommand();
        state.EndLocalCommand();
        state.SetLocalStatus(DeviceStatusType.Closed);
        Assert.False(state.OpensLocally);
        Assert.False(state.RoutesLocally);
        Assert.Equal(DeviceStatusType.Closed, state.Status);
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
        state.SetPreference(false);
        state.ObserveService(heartbeat);
        Assert.Equal(heartbeat, state.ServiceStatus);
        Assert.Equal(DeviceStatusType.Opened, state.Status);
        Assert.True(state.RoutesLocally);
        Assert.Throws<InvalidOperationException>(() => state.BeginServiceCommand("GetData"));
        state.BeginLocalCommand();
        state.EndLocalCommand();
    }

    [Fact]
    public void ServiceLossDoesNotReleaseObservedOwnership()
    {
        var state = new CameraBackendState(false);
        state.ObserveService(DeviceStatusType.Opened);
        state.ObserveService(DeviceStatusType.OffLine);
        Assert.Throws<InvalidOperationException>(() => state.BeginLocalOpen());
        state.SetPreference(true);
        Assert.False(state.OpensLocally);
        state.ObserveService(DeviceStatusType.Closed);
        state.SetPreference(true);
        state.BeginLocalOpen();
        Assert.True(state.LocalOwned);
    }

    [Fact]
    public void PendingCommandsAndVideoAllowPreferenceChangesButBlockOtherBackend()
    {
        var state = new CameraBackendState(false);
        state.BeginServiceCommand("GetData");
        Assert.Throws<InvalidOperationException>(() => state.BeginLocalOpen());
        state.SetPreference(true);
        state.EndServiceCommand();
        state.BeginLocalOpen(video: true);
        Assert.Throws<InvalidOperationException>(() => state.BeginServiceCommand("Open"));
        Assert.Throws<InvalidOperationException>(() => state.BeginLocalOpen());
        state.SetPreference(false);
        state.EndVideo();
        state.SetPreference(true);
        state.BeginLocalCommand();
        state.SetPreference(false);
        Assert.Throws<InvalidOperationException>(() => state.BeginLocalCommand());
        state.EndLocalCommand();
        state.SetPreference(false);
        Assert.False(state.RoutesLocally);
    }

    [Fact]
    public void LocalOwnershipOverridesLogicalServiceUntilLocalClose()
    {
        var state = new CameraBackendState(true);
        state.BeginLocalOpen();
        state.SetLocalStatus(DeviceStatusType.Opened);
        state.ObserveService(DeviceStatusType.Opened);
        state.BeginLocalCommand();
        state.EndLocalCommand();
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
        Assert.Equal(state.ServiceStatus, state.Status);
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
