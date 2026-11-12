using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Tif;
using cvColorVision;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public class LocalCalibrationRoiTests
{
    [Fact]
    public void ResolveUsesPhysicalCameraRoiForMatchingRawDimensions()
    {
        PhyCameraCfg cameraConfig = new()
        {
            PointX = 5000,
            PointY = 2200,
            Width = 800,
            Height = 700
        };

        LocalCalibrationRoi roi = LocalCalibrationRoi.ResolveForFrame(new LocalFrameMetadata { Width = 800, Height = 700 }, cameraConfig);

        Assert.True(roi.IsConfigured);
        Assert.Equal(5000, roi.X);
        Assert.Equal(2200, roi.Y);
        Assert.Equal(800, roi.Width);
        Assert.Equal(700, roi.Height);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiveFrameCalibrationUsesTheRoiSentWhenTheCameraOpened(bool cropped)
    {
        var cameraConfig = new PhyCameraCfg
        {
            PointX = cropped ? 2 : 0,
            PointY = cropped ? 3 : 0,
            Width = cropped ? 4 : 0,
            Height = cropped ? 3 : 0
        };
        JObject openedConfig = JObject.Parse(LocalCameraSession.BuildCameraConfigurationJson(cameraConfig));
        var native = new LocalCameraSessionTests.FakeNative { OpenedCalibrationRoi = LocalCameraNative.ReadCalibrationRoi(openedConfig) };
        using var session = new LocalCameraSession(native, new CameraBackendState(true));
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("camera", TakeImageMode.Measure_Normal, 8));
        using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = 4, Height = 3, SourceBpp = 8, Channels = 1,
            Acquisition = new ImageAcquisitionRecord { SensorRoi = session.OpenedCalibrationRoi.ToAcquisitionRoi() }
        }, 12, 0);

        cameraConfig.PointX = 7;
        cameraConfig.PointY = 8;
        cameraConfig.Width = 4;
        cameraConfig.Height = 3;
        frame.PrepareForCalibration("test", 12 * sizeof(float), hasBasicCalibration: false);

        LocalCalibrationRoi roi = LocalCalibrationRoi.ResolveForFrame(frame.Metadata, cameraConfig);
        var options = OpenCvLocalCalibrationCache.CreateExecutionOptions([10], roi);
        Assert.Equal(cropped ? 2u : 0u, options.RoiX);
        Assert.Equal(cropped ? 3u : 0u, options.RoiY);
        Assert.Equal(cropped ? 4u : 0u, options.RoiWidth);
        Assert.Equal(cropped ? 3u : 0u, options.RoiHeight);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(false, null)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(true, null)]
    public void SavedFrameRoiSurvivesLoadingAndGuidesRawRecalibration(bool cvcie, bool? cropped)
    {
        const int width = 4, height = 3;
        LocalCalibrationRoi originalRoi = cropped == true ? new(2, 3, width, height) : default;
        var acquisition = new ImageAcquisitionRecord { SensorRoi = originalRoi.ToAcquisitionRoi(), Width = width, Height = height };
        var currentConfig = new PhyCameraCfg { PointX = 7, PointY = 8, Width = width, Height = height };
        string directory = Directory.CreateTempSubdirectory("cv-file-roi-").FullName;
        string path = Path.Combine(directory, cvcie ? "capture.cvcie" : "capture.cvraw");
        bool cacheEnabled = CVFileReadCache.IsEnabled;
        CVFileReadCache.IsEnabled = false;
        try
        {
            if (cvcie)
            {
                using var cie = new CVCIEFile { Version = 2, FileExtType = CVType.CIE, Cols = width, Rows = height, Bpp = 32, Channels = 1, Exp = [10], Data = new byte[width * height * sizeof(float)] };
                Assert.True(CVFileUtil.WriteCIEFile(path, cie));
                if (cropped.HasValue) CameraFileMetadata.Save(path, CameraFileMetadata.AcquisitionKind, acquisition);
            }
            else
            {
                byte[] raw = Enumerable.Repeat((byte)10, width * height).ToArray();
                raw[0] = 100;
                using var source = LocalFlowFrame.Allocate(new LocalFrameMetadata
                {
                    Width = width, Height = height, SourceBpp = 8, Channels = 1, Exposure = [10],
                    Acquisition = cropped.HasValue ? acquisition : null
                }, raw.Length, 0);
                using (LocalFlowFrameLease lease = source.Acquire()) Marshal.Copy(raw, 0, lease.RawPointer, raw.Length);
                LocalFrameFileService.SaveCapture(source, path);
            }
            if (!cropped.HasValue) CameraFileMetadata.Save(path, CameraFileMetadata.AcquisitionKind, new { Width = width, Height = height });

            using LocalFlowFrame frame = LocalFrameFileService.Load(path, exposureOverride: [10], gainOverride: 1);
            Assert.Equal(cropped.HasValue ? acquisition.SensorRoi : null, frame.Metadata.Acquisition!.SensorRoi);
            LocalCalibrationRoi roi = LocalCalibrationRoi.ResolveForFrame(frame.Metadata, currentConfig);
            if (cvcie)
            {
                Assert.True(frame.HasCie);
                Assert.False(frame.HasRaw);
            }
            else
            {
                string defects = Path.Combine(directory, "defects.dat");
                using (var writer = new BinaryWriter(File.Create(defects)))
                {
                    writer.Write(2u);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write(3u);
                    writer.Write(2u);
                }
                using var cache = new LocalCalibrationCacheManager("file-roi");
                LocalFrameCalibrationService.CalibrateInPlace(frame, cache,
                    [new DeviceCameraCalibrationFile("defect", CalibrationType.DefectPoint, "defects", "defects.dat", defects)], "defects", roi);
                using LocalFlowFrameLease lease = frame.Acquire();
                Assert.Equal(cropped.HasValue ? 10 : 100, Marshal.ReadByte(lease.RawPointer));
            }
            Assert.Equal(cropped.HasValue ? originalRoi : new LocalCalibrationRoi(7, 8, width, height), roi);
        }
        finally
        {
            CVFileReadCache.IsEnabled = cacheEnabled;
            foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void ResolveKeepsFullFrameCalibrationForDifferentImageDimensions()
    {
        PhyCameraCfg cameraConfig = new()
        {
            PointX = 5000,
            PointY = 2200,
            Width = 800,
            Height = 700
        };

        LocalCalibrationRoi roi = LocalCalibrationRoi.Resolve(cameraConfig, 9568, 6380);

        Assert.False(roi.IsConfigured);
    }

    [Fact]
    public void ExecutionOptionsCarryRoiToNativeCalibration()
    {
        LocalCalibrationRoi roi = new(5000, 2200, 800, 700);

        var options = OpenCvLocalCalibrationCache.CreateExecutionOptions(new[] { 100f, 101f, 102f }, roi);

        Assert.Equal((uint)5000, options.RoiX);
        Assert.Equal((uint)2200, options.RoiY);
        Assert.Equal((uint)800, options.RoiWidth);
        Assert.Equal((uint)700, options.RoiHeight);
    }
}
