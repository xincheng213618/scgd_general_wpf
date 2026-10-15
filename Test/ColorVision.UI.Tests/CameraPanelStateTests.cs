using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera.Controls;
using ColorVision.Engine.Services.Devices.Camera.Local;

namespace ColorVision.UI.Tests;

public class CameraPanelStateTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosedCameraOffersBothCaptureAndVideoEntry(bool preferLocal)
    {
        var backend = new CameraBackendState(preferLocal);
        backend.ObserveService(DeviceStatusType.Closed);
        var closed = CameraPanelState.Create(backend, false, false);
        Assert.True(closed.CanConnect);
        Assert.True(closed.CanPreview);
        Assert.False(closed.Connected);
        Assert.False(closed.CanCapture);
    }

    [Fact]
    public void LocalOwnerKeepsCaptureAndPreviewWhenPreferenceAndServiceHeartbeatChange()
    {
        var backend = new CameraBackendState(true);
        backend.SetLocalStatus(DeviceStatusType.LiveOpened);
        backend.SetPreference(false);
        backend.ObserveService(DeviceStatusType.OffLine);
        var preview = CameraPanelState.Create(backend, previewing: true, operating: false);
        Assert.True(preview.CanCapture);
        Assert.True(preview.CanPreview);
        Assert.False(preview.CanConnect);
        Assert.False(preview.ShowServiceSettings);
        Assert.Equal("CameraPanel_Previewing", preview.StatusKey);

        var stopped = CameraPanelState.Create(backend, previewing: false, operating: false);
        Assert.True(stopped.Connected);
        Assert.True(stopped.CanCapture);
        Assert.Equal("CameraPanel_Ready", stopped.StatusKey);
    }

    [Fact]
    public void ServiceOwnerAndLegacyVideoRequireExplicitReleaseBeforeSwitchingBackend()
    {
        var backend = new CameraBackendState(true);
        backend.ObserveService(DeviceStatusType.Opened);
        var service = CameraPanelState.Create(backend, false, false);
        Assert.True(service.CanCapture);
        Assert.False(service.CanPreview);
        backend.ObserveService(DeviceStatusType.OffLine);
        Assert.False(CameraPanelState.Create(backend, false, false).CanConnect);
        Assert.True(CameraPanelState.Create(backend, false, false).Connected);
        Assert.Equal("DeviceStatusOffLine", CameraPanelState.Create(backend, false, false).StatusKey);
        backend.ObserveService(DeviceStatusType.Closed);
        backend.BeginLocalOpen(video: true);
        var video = CameraPanelState.Create(backend, true, false);
        Assert.True(video.Connected);
        Assert.False(video.CanPreview);
        Assert.False(video.CanCapture);
        Assert.False(video.CanConnect);
        // A failed close may retain the handle after the callback has stopped.
        Assert.False(CameraPanelState.Create(backend, false, false).CanPreview);
        backend.EndVideo();
        Assert.True(CameraPanelState.Create(backend, false, false).CanConnect);
        Assert.True(CameraPanelState.Create(backend, false, false).CanPreview);
    }

    [Theory]
    [InlineData(DeviceStatusType.Opened)]
    [InlineData(DeviceStatusType.LiveOpened)]
    public void ActiveOperationDisablesConflictingActionsUntilCompletion(DeviceStatusType status)
    {
        var backend = new CameraBackendState(true);
        backend.SetLocalStatus(status);
        var busy = CameraPanelState.Create(backend, true, true);
        Assert.True(busy.Busy);
        Assert.True(busy.ShowParameters);
        Assert.False(busy.CanConnect);
        Assert.False(busy.CanPreview);
        Assert.False(busy.CanCapture);
        Assert.True(CameraPanelState.Create(backend, false, false).CanCapture);
    }
}
