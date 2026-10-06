using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using cvColorVision;
using ColorVision.Engine.Services.PhyCameras.Configs;
using Newtonsoft.Json.Linq;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using System.Runtime.InteropServices;
using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public class LocalCameraSessionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyIdDiscoversOneCameraAndSavesItForReuse(string? configuredId)
    {
        ConfigCamera config = new() { CameraID = configuredId! };
        var native = new FakeNative { CameraIds = ["camera-1"] };
        int saves = 0;
        using var session = new LocalCameraSession(native, new CameraBackendState(true), config, () => saves++);

        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open(configuredId!, TakeImageMode.Measure_Normal, 16));
        Assert.Equal("camera-1", native.OpenedId);
        Assert.Equal("camera-1", config.CameraID);
        Assert.Equal(1, saves);
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Equal(1, native.Scans);
        Assert.Equal(1, native.Opens);

        session.Close(true);
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open(config.CameraID, TakeImageMode.Measure_Normal, 16));
        Assert.Equal(1, native.Scans);
        Assert.Equal(2, native.Opens);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void EmptyIdUsesCameraCodeToChooseAmongMultipleCameras()
    {
        string cameraCode = ColorVision.Common.Utilities.Tool.GetMD5("camera-2").ToLowerInvariant();
        ConfigCamera config = new() { CameraCode = cameraCode };
        var native = new FakeNative { CameraIds = ["camera-1", "camera-2"] };
        using var session = new LocalCameraSession(native, new CameraBackendState(true), config);

        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Equal("camera-2", native.OpenedId);
        Assert.Equal(cameraCode, config.CameraCode);
    }

    [Fact]
    public void MultipleUnboundCamerasRequireAnExplicitSelectionAndAllowRetry()
    {
        ConfigCamera config = new();
        var native = new FakeNative { CameraIds = ["camera-1", "camera-2"] };
        var backend = new CameraBackendState(true);
        int saves = 0;
        using var session = new LocalCameraSession(native, backend, config, () => saves++);

        Assert.Throws<InvalidOperationException>(() => session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Equal(0, native.Initializations);
        Assert.Equal(0, native.Opens);
        Assert.False(backend.LocalOwned);
        Assert.Null(config.CameraID);
        Assert.Equal(0, saves);

        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("camera-2", TakeImageMode.Measure_Normal, 16));
        Assert.Equal("camera-2", config.CameraID);
        Assert.Equal(1, native.Scans);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void MissingBoundCameraDoesNotOpenAnotherCamera()
    {
        ConfigCamera config = new() { CameraCode = ColorVision.Common.Utilities.Tool.GetMD5("missing") };
        var native = new FakeNative { CameraIds = ["camera-1"] };
        using var session = new LocalCameraSession(native, new CameraBackendState(true), config);

        Assert.Throws<InvalidOperationException>(() => session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Equal(0, native.Opens);
        Assert.Null(config.CameraID);
    }

    [Fact]
    public void ExistingConfiguredIdDoesNotScanOrReplaceTheBinding()
    {
        ConfigCamera config = new() { CameraID = "configured", CameraCode = "existing-code" };
        var native = new FakeNative { CameraIds = ["other"] };
        using var session = new LocalCameraSession(native, new CameraBackendState(true), config);

        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Equal("configured", native.OpenedId);
        Assert.Equal(0, native.Scans);
        Assert.Equal("existing-code", config.CameraCode);
    }

    [Fact]
    public void FailedNativeOpenRestoresEmptyIdAndDoesNotSaveIt()
    {
        ConfigCamera config = new();
        var native = new FakeNative { CameraIds = ["camera-1"], OpenResult = -1 };
        var backend = new CameraBackendState(true);
        int saves = 0;
        using var session = new LocalCameraSession(native, backend, config, () => saves++);

        Assert.Equal(-1, session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Null(config.CameraID);
        Assert.Equal(0, saves);
        Assert.False(backend.LocalOwned);

        native.OpenResult = cvErrorDefine.CV_ERR_SUCCESS;
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Equal("camera-1", config.CameraID);
        Assert.Equal(1, saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingCameraOrFailedDiscoveryDoesNotInitializeOrReserveOwnership(bool discoveryFails)
    {
        var native = new FakeNative { FailDiscovery = discoveryFails };
        var backend = new CameraBackendState(true);
        using var session = new LocalCameraSession(native, backend);

        Assert.Throws<InvalidOperationException>(() => session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Equal(0, native.Initializations);
        Assert.Equal(0, native.Opens);
        Assert.False(backend.LocalOwned);
    }

    [Fact]
    public void ServiceOwnerBlocksBeforeAutomaticDiscovery()
    {
        var native = new FakeNative { CameraIds = ["camera-1"] };
        var backend = new CameraBackendState(true);
        backend.ObserveService(ColorVision.Engine.Services.DeviceStatusType.Opened);
        using var session = new LocalCameraSession(native, backend);

        Assert.Throws<InvalidOperationException>(() => session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Equal(0, native.Scans);
        Assert.Equal(0, native.Opens);
    }

    [Fact]
    public void DiscoveredIdentityIsCheckedForOwnershipBeforeNativeOpen()
    {
        ConfigCamera config = new();
        var native = new FakeNative { CameraIds = ["camera-1"] };
        var backend = new CameraBackendState(true);
        using var session = new LocalCameraSession(native, backend, config, ensureAvailable: id =>
        {
            if (id == "camera-1") throw new InvalidOperationException("already owned");
        });

        Assert.Throws<InvalidOperationException>(() => session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Null(config.CameraID);
        Assert.Equal(0, native.Initializations);
        Assert.False(backend.LocalOwned);
    }

    [Fact]
    public void SaveFailureKeepsTheRealOpenedSessionUsable()
    {
        ConfigCamera config = new();
        var native = new FakeNative { CameraIds = ["camera-1"] };
        using var session = new LocalCameraSession(native, new CameraBackendState(true), config,
            () => throw new InvalidOperationException("save failed"));

        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.True(session.IsOpen);
        Assert.Equal("camera-1", config.CameraID);
        Assert.Equal(new IntPtr(42), session.UseOpened(handle => handle));
    }

    [Fact]
    public void DiscoveryIgnoresBlankAndDuplicateIds()
    {
        var native = new FakeNative { CameraIds = [" ", " camera-1 ", "CAMERA-1"] };
        using var session = new LocalCameraSession(native, new CameraBackendState(true));

        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.Equal("camera-1", native.OpenedId);
    }

    [Fact]
    public void CameraConfigurationJson_MapsPhysicalCameraSettingsAndRoi()
    {
        PhyCameraCfg config = new()
        {
            Ob = 4,
            ObR = 5,
            ObT = 6,
            ObB = 7,
            TempCtlChecked = true,
            TargetTemp = 8.5f,
            TempSpanTime = 42,
            UsbTraffic = 9.5f,
            Offset = 10,
            Gain = 11,
            PointX = 12,
            PointY = 13,
            Width = 640,
            Height = 480,
            SensorWidth = 9568,
            SensorHeight = 6380
        };

        JObject root = JObject.Parse(LocalCameraSession.BuildCameraConfigurationJson(config));
        JObject cameraCfg = Assert.IsType<JObject>(root["cameraCfg"]);

        Assert.Equal(4, cameraCfg.Value<int>("ob"));
        Assert.Equal(5, cameraCfg.Value<int>("obR"));
        Assert.Equal(6, cameraCfg.Value<int>("obT"));
        Assert.Equal(7, cameraCfg.Value<int>("obB"));
        Assert.True(cameraCfg.Value<bool>("tempCtlChecked"));
        Assert.Equal(8.5f, cameraCfg.Value<float>("targetTemp"));
        Assert.Equal(42, cameraCfg.Value<int>("TempSpanTime"));
        Assert.Equal(9.5f, cameraCfg.Value<float>("usbTraffic"));
        Assert.Equal(10, cameraCfg.Value<int>("offset"));
        Assert.Equal(11, cameraCfg.Value<int>("gain"));
        Assert.Equal(12, cameraCfg.Value<int>("ex"));
        Assert.Equal(13, cameraCfg.Value<int>("ey"));
        Assert.Equal(640, cameraCfg.Value<int>("ew"));
        Assert.Equal(480, cameraCfg.Value<int>("eh"));
        Assert.Equal(14, cameraCfg.Properties().Count());
    }

    [Fact]
    public void CameraConfigurationJson_PreservesZeroRoiForFullFrame()
    {
        PhyCameraCfg config = new()
        {
            PointX = 0,
            PointY = 0,
            Width = 0,
            Height = 0
        };

        JObject root = JObject.Parse(LocalCameraSession.BuildCameraConfigurationJson(config));
        JObject cameraCfg = Assert.IsType<JObject>(root["cameraCfg"]);

        Assert.Equal(0, cameraCfg.Value<int>("ex"));
        Assert.Equal(0, cameraCfg.Value<int>("ey"));
        Assert.Equal(0, cameraCfg.Value<int>("ew"));
        Assert.Equal(0, cameraCfg.Value<int>("eh"));
    }

    [Fact]
    public void FlowFramesStayExclusiveUntilTheirLastLeaseIsReleased()
    {
        using var session = new LocalCameraSession(new FakeNative(), new CameraBackendState(true));
        session.Open("camera-1", TakeImageMode.Measure_Normal, 16);
        using var resources = new FlowRuntimeResources();
        using var first = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, session.RawBufferPool);
        using var second = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, session.RawBufferPool);
        resources.Set("first", first);
        resources.Set("second", second);
        using var firstLease = first.Acquire();
        using var secondLease = second.Acquire();
        IntPtr firstPointer = firstLease.RawPointer;
        IntPtr secondPointer = secondLease.RawPointer;
        Assert.NotEqual(firstPointer, secondPointer);
        Marshal.WriteByte(firstPointer, 11);
        Marshal.WriteByte(secondPointer, 22);

        resources.Dispose();
        Assert.Throws<ObjectDisposedException>(() => first.Acquire());
        using var third = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, session.RawBufferPool);
        using var thirdLease = third.Acquire();
        Assert.NotEqual(firstPointer, thirdLease.RawPointer);
        Assert.NotEqual(secondPointer, thirdLease.RawPointer);
        Assert.Equal(11, Marshal.ReadByte(firstPointer));
        Assert.Equal(22, Marshal.ReadByte(secondPointer));

        firstLease.Dispose();
        using var next = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, session.RawBufferPool);
        using var nextLease = next.Acquire();
        Assert.Equal(firstPointer, nextLease.RawPointer);
        Marshal.WriteByte(nextLease.RawPointer, 33);
        first.Dispose();
        firstLease.Dispose();
        Assert.Equal(33, Marshal.ReadByte(nextLease.RawPointer));
        Assert.Equal(22, Marshal.ReadByte(secondLease.RawPointer));
    }

    [Fact]
    public void ReusingRawMemoryCreatesFreshImageIdentityAndCalibrationState()
    {
        var timing = new FlowNodeTiming();
        using var activation = timing.Activate();
        using var session = new LocalCameraSession(new FakeNative(), new CameraBackendState(true));
        session.Open("camera-1", TakeImageMode.Measure_Normal, 16);
        using var first = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = 2, Height = 1, Channels = 1, SourceBpp = 16,
            PrimaryBufferKind = LocalFrameBufferKind.CvRaw, FlipMode = CVImageFlipMode.Y,
            Exposure = [10], CalibrationTemplate = "previous", IsMirrorReady = true
        }, 4, 8, session.RawBufferPool);
        first.MasterId = 42;
        first.CvRawFilePath = "previous.cvraw";
        first.MarkPrimaryBufferFlipApplied();
        IntPtr pointer;
        using (var lease = first.Acquire()) pointer = lease.RawPointer;
        first.Dispose();

        LocalFrameMetadata metadata = new()
        {
            Width = 2, Height = 1, Channels = 1, SourceBpp = 16,
            PrimaryBufferKind = LocalFrameBufferKind.CvRaw, FlipMode = CVImageFlipMode.Y,
            Exposure = [20], IsMirrorReady = true
        };
        using var next = LocalFlowFrame.Allocate(metadata, 4, 0, session.RawBufferPool);
        using var nextLease = next.Acquire();
        Assert.Equal(pointer, nextLease.RawPointer);
        Assert.NotEqual(first.FrameId, next.FrameId);
        Assert.Same(metadata, next.Metadata);
        Assert.Equal(-1, next.MasterId);
        Assert.Empty(next.CvRawFilePath);
        Assert.Null(next.ColorCalibration);
        Assert.False(next.IsRawFlipApplied);
        Assert.False(next.HasCie);
        Assert.Equal(IntPtr.Zero, nextLease.CiePointer);
        Assert.Equal(new[] { "AllocateRawBuffer", "ReuseRawBuffer" }, timing.Finish().Stages.Select(stage => stage.Name));
    }

    [Fact]
    public void SizeChangesPreserveTheNewBufferWhenAnOlderSizeReturnsLate()
    {
        using var session = new LocalCameraSession(new FakeNative(), new CameraBackendState(true));
        session.Open("camera-1", TakeImageMode.Measure_Normal, 16);
        using var old = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, session.RawBufferPool);
        using var oldLease = old.Acquire();
        Marshal.WriteByte(oldLease.RawPointer, 15, 17);
        IntPtr currentPointer;
        using (var current = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 64, 0, session.RawBufferPool))
        using (var currentLease = current.Acquire())
        {
            currentPointer = currentLease.RawPointer;
            Assert.NotEqual(oldLease.RawPointer, currentPointer);
            Marshal.WriteByte(currentPointer, 63, 29);
            Assert.Equal(17, Marshal.ReadByte(oldLease.RawPointer, 15));
        }
        old.Dispose();
        oldLease.Dispose();

        using var next = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 64, 0, session.RawBufferPool);
        using var nextLease = next.Acquire();
        Assert.Equal(currentPointer, nextLease.RawPointer);
        Assert.Equal(64, nextLease.RawLength);
        Assert.Equal(29, Marshal.ReadByte(nextLease.RawPointer, 63));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingOrDisposingTheCameraRetiresItsPoolButKeepsOutstandingImagesValid(bool dispose)
    {
        using var session = new LocalCameraSession(new FakeNative(), new CameraBackendState(true));
        session.Open("camera-1", TakeImageMode.Measure_Normal, 16);
        var oldPool = session.RawBufferPool;
        using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, oldPool);
        using var lease = frame.Acquire();
        Marshal.WriteByte(lease.RawPointer, 37);
        using (var idle = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, oldPool)) { }

        if (dispose) session.Dispose();
        else session.Close(true);
        Assert.Throws<ObjectDisposedException>(() => oldPool.Rent(16));
        Assert.Equal(37, Marshal.ReadByte(lease.RawPointer));
        if (!dispose)
        {
            session.Open("camera-2", TakeImageMode.Measure_Normal, 16);
            Assert.NotSame(oldPool, session.RawBufferPool);
            using var next = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, session.RawBufferPool);
            using var nextLease = next.Acquire();
            Assert.NotEqual(lease.RawPointer, nextLease.RawPointer);
            Marshal.WriteByte(nextLease.RawPointer, 41);
            frame.Dispose();
            lease.Dispose();
            Assert.Equal(41, Marshal.ReadByte(nextLease.RawPointer));
            Assert.Throws<ObjectDisposedException>(() => oldPool.Rent(16));
        }
    }

    [Fact]
    public void FailedCloseKeepsTheExistingPoolUsableUntilTheCameraActuallyCloses()
    {
        var native = new FakeNative { IgnoreClose = true };
        using var session = new LocalCameraSession(native, new CameraBackendState(true));
        session.Open("camera-1", TakeImageMode.Measure_Normal, 16);
        var pool = session.RawBufferPool;
        IntPtr pointer;
        using (var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, pool))
        using (var lease = frame.Acquire()) pointer = lease.RawPointer;
        Assert.Throws<InvalidOperationException>(() => session.Close(true));
        Assert.Same(pool, session.RawBufferPool);
        using (var next = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 16, 0, pool))
        using (var lease = next.Acquire()) Assert.Equal(pointer, lease.RawPointer);

        native.IgnoreClose = false;
        session.Close(true);
        Assert.Throws<ObjectDisposedException>(() => pool.Rent(16));
    }

    private sealed class FakeNative : ILocalCameraNative
    {
        public IReadOnlyList<string> CameraIds { get; init; } = [];
        public bool FailDiscovery { get; init; }
        public bool IgnoreClose { get; set; }
        public int Scans, Initializations, Opens;
        public int OpenResult = cvErrorDefine.CV_ERR_SUCCESS;
        public string? OpenedId;
        private bool opened;

        public IReadOnlyList<string> GetCameraIds()
        {
            Scans++;
            if (FailDiscovery) throw new InvalidOperationException("scan failed");
            return CameraIds;
        }
        public IntPtr Initialize() { Initializations++; return new IntPtr(42); }
        public bool IsOpen(IntPtr handle) => opened;
        public int Open(IntPtr handle, string cameraId, TakeImageMode mode, int bpp)
        {
            Opens++;
            OpenedId = cameraId;
            opened = OpenResult == cvErrorDefine.CV_ERR_SUCCESS;
            return OpenResult;
        }
        public void Close(IntPtr handle) { if (!IgnoreClose) opened = false; }
        public void DetachCallback(IntPtr handle) { }
        public bool UpdateCalibration(IntPtr handle, string json) => true;
        public void Release(IntPtr handle) { }
    }
}
