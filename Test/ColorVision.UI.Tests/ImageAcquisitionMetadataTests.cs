using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Tif;
using System.Drawing.Imaging;
using cvColorVision;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

/// <summary>Protects persisted acquisition provenance independently of measurement and legacy payload readers.</summary>
public sealed class ImageAcquisitionMetadataTests
{
    [Fact]
    public void BasicCalibrationProvenanceBelongsToSavedOutputAndDoesNotRelabelUnmodifiedInput()
    {
        string root = Path.Combine(Path.GetTempPath(), "cv-basic-provenance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string input = Path.Combine(root, "input.cvraw"), output = Path.Combine(root, "output.cvraw"), dark = Path.Combine(root, "dark.dat");
            File.WriteAllText(dark, """{"bpp":16,"Texp_x":1.0,"DarkNoiseRatio":2.0}""");
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 2, 16, 1);
            Assert.True(CVFileUtil.WriteCVRaw(input, raw));
            byte[] original = File.ReadAllBytes(input);
            using LocalFlowFrame frame = LocalFrameFileService.Load(input);
            using LocalCalibrationCacheManager manager = new("basic-provenance");
            LocalFrameCalibrationService.CalibrateInPlace(frame, manager,
                [new DeviceCameraCalibrationFile("dark", CalibrationType.DarkNoise, "暗噪声", "dark.dat", dark)], "基础校正", default);
            Assert.Null(frame.ColorCalibration);
            Assert.Equal(original, File.ReadAllBytes(input));
            LocalFrameFileService.SaveCapture(frame, output);
            using LocalFlowFrame reopened = LocalFrameFileService.Load(output);
            Assert.Null(reopened.ColorCalibration);
            Assert.Equal("基础校正", reopened.CalibrationProvenance!.Template);
            Assert.Equal("dark.dat", Assert.Single(reopened.CalibrationProvenance.Sources).FileName);
            Assert.NotNull(reopened.CalibrationProvenance.Sources[0].FileSha256);
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcquisitionAndExecutedCalibrationSurviveRawSaveAndLoad(bool memoryOnly)
    {
        string root = Path.Combine(Path.GetTempPath(), "cv-acquisition-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        bool cacheEnabled = CVFileReadCache.IsEnabled;
        try
        {
            if (memoryOnly) CVFileReadCache.IsEnabled = true;
            string calibration = Path.Combine(root, "色度.dat"), path = Path.Combine(root, "capture.cvraw");
            File.WriteAllText(calibration, """{"a":1,"b":0,"c":0,"d":0,"e":2,"f":0,"g":0,"h":0,"i":3}""");
            string expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(calibration)));
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 2, 16, 3);
            var acquisition = new ImageAcquisitionRecord
            {
                Camera = new() { DeviceName = "相机一", DeviceCode = "DEV.Camera.1", CameraId = "CAM-123", Model = "HK_USB" },
                CapturedUtc = new DateTime(2026, 10, 23, 0, 1, 2, DateTimeKind.Utc),
                Width = 3, Height = 2, BitsPerChannel = 16, Channels = 3,
                Exposure = [10, 20, 30], ExposureUnit = "ms", Gain = 2, NdPort = 4, AverageCount = 3
            };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 3, Height = 2, SourceBpp = 16, Channels = 3, Gain = 2,
                Exposure = [10, 20, 30], Acquisition = acquisition
            }, raw.Data.Length, 0);
            using (var lease = frame.Acquire()) Marshal.Copy(raw.Data, 0, lease.RawPointer, raw.Data.Length);
            using LocalCalibrationCacheManager manager = new("test-acquisition");
            LocalFrameCalibrationService.CalibrateInPlace(frame, manager,
                [new DeviceCameraCalibrationFile("color", CalibrationType.LumFourColor, "色度", "色度.dat", calibration)], "模板A", default, allowAcceleration: true);
            var provenance = Assert.IsType<ImageCalibrationSummary>(frame.CalibrationProvenance);
            Assert.Equal(expectedHash, Assert.Single(provenance.Sources).FileSha256);
            // Saving must retain the resource actually loaded, even after the external template changes.
            File.WriteAllText(calibration, "{}");
            LocalFrameFileService.SaveCapture(frame, path, memoryOnly ? CVFileSaveMode.MemoryOnly : CVFileSaveMode.Synchronous);
            CVFileMetadata.SetProperty(path, "future.record", 77, [1, 2]);
            frame.ColorCalibration!.Save(path, true);
            using LocalFlowFrame reopened = LocalFrameFileService.Load(path);
            Assert.Equal("CAM-123", reopened.Metadata.Acquisition!.Camera!.CameraId);
            Assert.Equal(acquisition.CapturedUtc, reopened.Metadata.Acquisition.CapturedUtc);
            Assert.Equal(4, reopened.Metadata.Acquisition.NdPort);
            Assert.Equal(new float[] { 10, 20, 30 }, reopened.Metadata.Acquisition.Exposure);
            Assert.Equal(expectedHash, Assert.Single(reopened.CalibrationProvenance!.Sources).FileSha256);
            Assert.Equal("色度.dat", reopened.CalibrationProvenance.Sources[0].FileName);
            Assert.Equal(77u, CVFileMetadata.Read(path)["future.record"].Version);
            using CVCIEFile loaded = CVFileUtil.OpenLocalCVFile(path);
            Assert.Equal(1u, loaded.Version);
            Assert.Equal(raw.Data, loaded.Data);
            var parameters = VExportCIE.CreateTiffParameters(loaded, "Src");
            Assert.Equal(4, parameters.NdPort);
            Assert.Equal("CAM-123", parameters.Acquisition!.Camera!.CameraId);
            VExportCIE export = new(path)
            {
                SavePath = root, Name = "measurement", ExportImageFormat = ImageFormat.Png,
                IsExportSrc = true, IsExportChannelX = false, IsExportChannelY = false, IsExportChannelZ = false
            };
            VExportCIE.SaveToTifOrThrow(export);
            WpfTestHost.Invoke(() =>
            {
                using var stream = File.OpenRead(Path.Combine(root, "measurement.png"));
                var pixels = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                Assert.True(ColorVisionTiffParameters.TryRead(pixels.Metadata as BitmapMetadata, out var exported));
                Assert.Equal("CAM-123", exported!.Acquisition!.Camera!.CameraId);
                Assert.Equal(expectedHash, Assert.Single(exported.CalibrationSummary!.Sources).FileSha256);
                Assert.NotNull(exported.Calibration);
                Assert.Equal(ColorVisionPixelBinding.ComputeSha256(pixels), exported.PixelSha256);
            });
            if (memoryOnly) Assert.False(File.Exists(path));
        }
        finally
        {
            CVFileReadCache.Release(); CVFileReadCache.IsEnabled = cacheEnabled;
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("future")]
    [InlineData("invalid")]
    public void MissingUnknownAndInvalidOptionalRecordsDoNotChangeLegacyPixels(string mode)
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-acquisition-{Guid.NewGuid():N}.cvraw");
        try
        {
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 2, 16, 3);
            Assert.True(CVFileUtil.WriteCVRaw(path, raw));
            if (mode != "legacy") CVFileMetadata.SetProperty(path, CameraFileMetadata.AcquisitionKind,
                mode == "future" ? 99u : 1u, Encoding.UTF8.GetBytes(mode == "future" ? "{}" : "{broken"));
            if (mode != "legacy") CVFileMetadata.SetProperty(path, CameraFileMetadata.ProvenanceKind,
                mode == "future" ? 99u : 1u, Encoding.UTF8.GetBytes("{\"Template\":\"independent\"}"));
            byte[] original = File.ReadAllBytes(path);
            using var frame = LocalFrameFileService.Load(path);
            Assert.Null(frame.Metadata.Acquisition);
            if (mode == "invalid") Assert.Equal("independent", frame.CalibrationProvenance!.Template);
            else Assert.Null(frame.CalibrationProvenance);
            using var lease = frame.Acquire();
            Assert.Equal(raw.Data, lease.CopyRawToArray());
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }
}
