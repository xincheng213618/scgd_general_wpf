using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera.Local;
using cvColorVision;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public class LocalCameraOwnershipTests
{
    [Fact]
    public async Task ExistingCameraCommandApiRoutesLocalOpenAndCloseWithoutMqtt()
    {
        var native = new FakeNative();
        var backend = new CameraBackendState(true);
        using var session = new LocalCameraSession(native, backend);
        var device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
        device.SysResourceModel = new SysResourceModel { Code = "local-test" };
        typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, backend);
        typeof(DeviceCamera).GetField("<LocalCameraSession>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, session);
        var mqtt = (MQTTCamera)RuntimeHelpers.GetUninitializedObject(typeof(MQTTCamera));
        mqtt.Device = device;
        // A publish would dereference its absent MQTT client. Only the local facade can succeed.
        async Task<MsgRecordState> Execute(Func<MsgRecord> start)
        {
            TaskCompletionSource<MsgRecordState> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            WpfTestHost.Invoke(() =>
            {
                var record = start();
                record.MsgRecordStateChanged += (_, state) => completion.TrySetResult(state);
            });
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(MsgRecordState.Success, await Execute(() => mqtt.Open("camera", TakeImageMode.Measure_Normal, 16)));
        Assert.Equal(MsgRecordState.Success, await Execute(() => mqtt.Open("camera", TakeImageMode.Measure_Normal, 16)));
        Assert.Equal(1, native.Opens);
        Assert.Equal(MsgRecordState.Success, await Execute(mqtt.Close));
        Assert.False(backend.LocalOwned);
        Assert.Equal(DeviceStatusType.UnInit, backend.ServiceStatus);
    }

    [Fact]
    public void OpenThenFlowUsesOneNativeHandleAndCloseAllowsReopen()
    {
        var native = new FakeNative();
        var state = new CameraBackendState(true);
        using var session = new LocalCameraSession(native, state);
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("camera", TakeImageMode.Measure_Normal, 16));
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("camera", TakeImageMode.Measure_Normal, 16));
        Assert.Equal(new IntPtr(42), session.UseOpened(handle => handle));
        Assert.Equal(1, native.Initializations);
        Assert.Equal(1, native.Opens);
        Assert.Equal(DeviceStatusType.Opened, state.Status);
        session.Close(true);
        Assert.False(session.IsOpen);
        Assert.Equal(DeviceStatusType.Closed, state.Status);
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        Assert.Equal(2, native.Opens);
    }

    [Fact]
    public void FailedOpenDoesNotCreateFakeOpenOrBlockRetry()
    {
        var native = new FakeNative { OpenResult = -1 };
        var state = new CameraBackendState(true);
        using var session = new LocalCameraSession(native, state);
        Assert.Equal(-1, session.Open("camera", TakeImageMode.Measure_Normal, 16));
        Assert.False(state.LocalOwned);
        Assert.Throws<InvalidOperationException>(() => session.UseOpened(_ => true));
        native.OpenResult = cvErrorDefine.CV_ERR_SUCCESS;
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        Assert.True(state.LocalOwned);
    }

    [Fact]
    public void FailedCloseRetainsRealOwner()
    {
        var native = new FakeNative { FailClose = true };
        var state = new CameraBackendState(true);
        using var session = new LocalCameraSession(native, state);
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        Assert.Throws<InvalidOperationException>(() => session.Close(true));
        Assert.True(state.LocalOwned);
        Assert.Throws<InvalidOperationException>(() => state.SetPreference(false));
        native.FailClose = false;
    }

    [Fact]
    public void ServiceOwnerBlocksBeforeNativeInitialization()
    {
        var native = new FakeNative();
        var state = new CameraBackendState(false);
        state.ObserveService(DeviceStatusType.Opened);
        using var session = new LocalCameraSession(native, state);
        Assert.Throws<InvalidOperationException>(() => session.Open("camera", TakeImageMode.Measure_Normal, 16));
        Assert.Equal(0, native.Initializations);
        Assert.Equal(0, native.Opens);
    }

    [Fact]
    public void DifferentOpenParametersCannotSilentlyReuseSession()
    {
        var native = new FakeNative();
        using var session = new LocalCameraSession(native, new CameraBackendState(true));
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        Assert.Throws<InvalidOperationException>(() => session.Open("other", TakeImageMode.Measure_Normal, 16));
        Assert.Throws<InvalidOperationException>(() => session.Open("camera", TakeImageMode.Live, 16));
        Assert.Throws<InvalidOperationException>(() => session.Open("camera", TakeImageMode.Measure_Normal, 8));
        Assert.Equal(1, native.Opens);
    }

    [Fact]
    public void CalibrationCacheInvalidatesOnReopenAndDisposeIsIdempotent()
    {
        var native = new FakeNative();
        var session = new LocalCameraSession(native, new CameraBackendState(true));
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        session.UpdateCalibration("{}");
        session.UpdateCalibration("{}");
        Assert.Equal(1, native.CalibrationUpdates);
        session.Close(true);
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        session.UpdateCalibration("{}");
        Assert.Equal(2, native.CalibrationUpdates);
        session.Dispose();
        session.Dispose();
        Assert.Equal(1, native.Releases);
        Assert.Throws<ObjectDisposedException>(() => session.Open("camera", TakeImageMode.Measure_Normal, 16));
    }

    private sealed class FakeNative : ILocalCameraNative
    {
        public int Initializations, Opens, Releases, CalibrationUpdates;
        public int OpenResult = cvErrorDefine.CV_ERR_SUCCESS;
        public bool FailClose;
        private bool opened;
        public IntPtr Initialize() { Initializations++; return new IntPtr(42); }
        public bool IsOpen(IntPtr handle) => opened;
        public int Open(IntPtr handle, string cameraId, TakeImageMode mode, int bpp) { Opens++; opened = OpenResult == cvErrorDefine.CV_ERR_SUCCESS; return OpenResult; }
        public void Close(IntPtr handle) { if (!FailClose) opened = false; }
        public void DetachCallback(IntPtr handle) { }
        public bool UpdateCalibration(IntPtr handle, string json) { CalibrationUpdates++; return true; }
        public void Release(IntPtr handle) { Releases++; }
    }
}
