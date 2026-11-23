using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera.Local;
using cvColorVision;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public class LocalCameraOwnershipTests
{
    [Theory]
    [InlineData(TakeImageMode.Measure_Normal)]
    [InlineData(TakeImageMode.Live)]
    public async Task ExistingCameraCommandApiRoutesLocalOpenAndCloseWithoutMqtt(TakeImageMode initialMode)
    {
        var native = new FakeNative();
        var backend = new CameraBackendState(true);
        var config = new ConfigCamera { Code = "local-test", TakeImageMode = initialMode };
        using var session = new LocalCameraSession(native, backend, config);
        var device = CreateDevice(config, backend, session);
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
        Assert.Equal(MsgRecordState.Success, await Execute(() => mqtt.Open("camera", initialMode, 16)));
        Assert.Equal(TakeImageMode.Measure_Normal, config.TakeImageMode);
        backend.SetPreference(false);
        Assert.Equal(MsgRecordState.Success, await Execute(() => mqtt.Open("camera", TakeImageMode.Measure_Normal, 16)));
        Assert.Equal(1, native.Opens);
        Assert.Equal(MsgRecordState.Success, await Execute(mqtt.Close));
        Assert.False(backend.LocalOwned);
        Assert.Equal(DeviceStatusType.UnInit, backend.ServiceStatus);
    }

    [Fact]
    public async Task CloseWaitsForAnInFlightCaptureTimeoutAndBlocksNewCommands()
    {
        var native = new LocalCameraSessionTests.FakeNative();
        var backend = new CameraBackendState(true);
        var config = new ConfigCamera { Code = "local-test" };
        using var session = new LocalCameraSession(native, backend, config);
        session.Open("camera", TakeImageMode.Measure_Normal, 16);
        var device = CreateDevice(config, backend, session);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var captureCompleted = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeCompleted = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
        WpfTestHost.Invoke(() =>
        {
            var capture = device.RunLocalCommand("GetData", () => session.UseOpened<int>(_ =>
            {
                entered.SetResult();
                release.Wait();
                throw new TimeoutException("取图超时");
            }));
            capture.MsgRecordStateChanged += (_, state) => captureCompleted.TrySetResult(state);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                var close = device.CloseLocalCamera();
                close.MsgRecordStateChanged += (_, state) => closeCompleted.TrySetResult(state);
            });
            Assert.False(closeCompleted.Task.IsCompleted);
            Assert.Equal(0, native.Closes);
            Assert.Throws<InvalidOperationException>(backend.BeginLocalCommand);
            Assert.Throws<InvalidOperationException>(() => { _ = backend.BeginLocalClose(); });
        }
        finally { release.Set(); }

        Assert.Equal(MsgRecordState.Fail, await captureCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(MsgRecordState.Success, await closeCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, native.Closes);
        Assert.False(backend.IsCommandRunning);
        Assert.False(backend.LocalOwned);
    }

    [Fact]
    public async Task StoppingQueuedFlowCaptureDoesNotWaitForTheActiveModeSwitch()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseSwitch = new ManualResetEventSlim();
        var native = new LocalCameraSessionTests.FakeNative
        {
            SwitchResult = cvErrorDefine.CV_ERR_SUCCESS,
            OnSwitch = () => { entered.SetResult(); releaseSwitch.Wait(); }
        };
        var backend = new CameraBackendState(true);
        var config = new ConfigCamera { Code = "local-test", CameraID = "camera", ImageBpp = ImageBpp.bpp16 };
        using var session = new LocalCameraSession(native, backend, config);
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("camera", TakeImageMode.Live, 16));
        var device = CreateDevice(config, backend, session);
        using var activeStop = new CancellationTokenSource();
        using var queuedStop = new CancellationTokenSource();
        var services = new LocalCameraFlowServices();
        Task<LocalCameraCaptureResult> active = Task.Run(() => services.Capture(new() { Device = device, Stop = activeStop.Token }));
        Task<LocalCameraCaptureResult>? queued = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            queued = Task.Run(() =>
            {
                started.SetResult();
                return services.Capture(new() { Device = device, Stop = queuedStop.Token });
            });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            queuedStop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(active.IsCompleted);
            Assert.Equal(1, native.Switches);
        }
        finally
        {
            activeStop.Cancel();
            queuedStop.Cancel();
            releaseSwitch.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active.WaitAsync(TimeSpan.FromSeconds(10)));
            if (queued != null) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualCameraOperationDoesNotBlockCaptureCancellationOrOtherCameras(bool captureOtherCamera)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseManualOperation = new ManualResetEventSlim();
        using var queuedStop = new CancellationTokenSource();
        var config = new ConfigCamera { Code = "local-test", CameraID = "camera", ImageBpp = ImageBpp.bpp16 };
        var backend = new CameraBackendState(true);
        using var session = new LocalCameraSession(new FakeNative(), backend, config);
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("camera", TakeImageMode.Measure_Normal, 16));
        var device = CreateDevice(config, backend, session);
        Task<bool> manual = Task.Run(() => session.UseOpened(_ =>
        {
            entered.SetResult();
            releaseManualOperation.Wait();
            return true;
        }));
        Task<LocalCameraCaptureResult>? queued = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var worker = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
            queued = Task.Factory.StartNew(() =>
            {
                worker.SetResult(Thread.CurrentThread);
                return LocalCameraCaptureService.Capture(new() { Device = device, Stop = queuedStop.Token }, autoConnect: true);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Thread captureThread = await worker.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(SpinWait.SpinUntil(() => (captureThread.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(10)));
            Assert.False(queued.IsCompleted);

            if (captureOtherCamera)
            {
                using var otherStop = new CancellationTokenSource();
                var otherNative = new LocalCameraSessionTests.FakeNative
                {
                    SwitchResult = cvErrorDefine.CV_ERR_SUCCESS,
                    OnSwitch = otherStop.Cancel
                };
                var otherConfig = new ConfigCamera { Code = "other-test", CameraID = "other-camera", ImageBpp = ImageBpp.bpp16 };
                var otherBackend = new CameraBackendState(true);
                using var otherSession = new LocalCameraSession(otherNative, otherBackend, otherConfig);
                Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, otherSession.Open("other-camera", TakeImageMode.Live, 16));
                var otherDevice = CreateDevice(otherConfig, otherBackend, otherSession);
                Task<LocalCameraCaptureResult> otherCapture = Task.Run(() =>
                    LocalCameraCaptureService.Capture(new() { Device = otherDevice, Stop = otherStop.Token }, autoConnect: true));
                try
                {
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => otherCapture.WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.Equal(1, otherNative.Switches);
                    Assert.False(queued.IsCompleted);
                }
                finally
                {
                    otherStop.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => otherCapture.WaitAsync(TimeSpan.FromSeconds(10)));
                }
            }
            queuedStop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(manual.IsCompleted);
        }
        finally
        {
            queuedStop.Cancel();
            releaseManualOperation.Set();
            Assert.True(await manual.WaitAsync(TimeSpan.FromSeconds(10)));
            if (queued != null) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Fact]
    public void OpenThenFlowUsesOneNativeHandleAndCloseAllowsReopen()
    {
        var native = new FakeNative();
        var state = new CameraBackendState(true);
        state.ObserveService(DeviceStatusType.Closed);
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
        state.SetPreference(false);
        Assert.True(state.RoutesLocally);
        Assert.Equal(DeviceStatusType.Opened, state.Status);
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

    private static DeviceCamera CreateDevice(ConfigCamera config, CameraBackendState backend, LocalCameraSession session)
    {
        var device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
        device.Config = config;
        device.SysResourceModel = new SysResourceModel { Code = config.Code };
        typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, backend);
        typeof(DeviceCamera).GetField("<LocalCameraSession>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, session);
        return device;
    }

    private sealed class FakeNative : ILocalCameraNative
    {
        public int Initializations, Opens, Releases, CalibrationUpdates;
        public int OpenResult = cvErrorDefine.CV_ERR_SUCCESS;
        public bool FailClose;
        private bool opened;
        public LocalCalibrationRoi OpenedCalibrationRoi => default;
        public IReadOnlyList<string> GetCameraIds() => [];
        public IntPtr Initialize() { Initializations++; return new IntPtr(42); }
        public bool IsOpen(IntPtr handle) => opened;
        public void PrepareMeasurement(IntPtr handle) { }
        public int SwitchMode(IntPtr handle, TakeImageMode mode, int bpp) => cvErrorDefine.CV_ERR_CAM_TYPE_NOT;
        public int Open(IntPtr handle, string cameraId, (CameraModel Model, CameraMode Mode) cameraType, TakeImageMode mode, int bpp,
            bool useHikMvs, int hikBayerQuality, bool hikOutputBgr) { Opens++; opened = OpenResult == cvErrorDefine.CV_ERR_SUCCESS; return OpenResult; }
        public void Close(IntPtr handle) { if (!FailClose) opened = false; }
        public void DetachCallback(IntPtr handle) { }
        public bool UpdateCalibration(IntPtr handle, string json) { CalibrationUpdates++; return true; }
        public void Release(IntPtr handle) { Releases++; }
    }
}
