using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using cvColorVision;
using ColorVision.Engine.Services.PhyCameras.Configs;
using Newtonsoft.Json.Linq;

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

    private sealed class FakeNative : ILocalCameraNative
    {
        public IReadOnlyList<string> CameraIds { get; init; } = [];
        public bool FailDiscovery { get; init; }
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
        public void Close(IntPtr handle) => opened = false;
        public void DetachCallback(IntPtr handle) { }
        public bool UpdateCalibration(IntPtr handle, string json) => true;
        public void Release(IntPtr handle) { }
    }
}
