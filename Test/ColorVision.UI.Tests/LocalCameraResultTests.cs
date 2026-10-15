using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using FlowEngineLib.Algorithm;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using System.IO;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using Newtonsoft.Json;
using OpenCvSharp.WpfExtensions;

namespace ColorVision.UI.Tests;

public class LocalCameraResultTests
{
    [Fact]
    public void MainPanelLocalCaptureDefaultsToSavingAndPersistsOptOut()
    {
        Assert.True(new DisplayCameraConfig().SaveLocalCaptureFiles);
        Assert.True(JsonConvert.DeserializeObject<DisplayCameraConfig>("{\"UseLocalCamera\":true}")!.SaveLocalCaptureFiles);
        var config = new DisplayCameraConfig { SaveLocalCaptureFiles = false };
        Assert.False(JsonConvert.DeserializeObject<DisplayCameraConfig>(JsonConvert.SerializeObject(config))!.SaveLocalCaptureFiles);
    }

    [Fact]
    public void LegacyCameraFileSaveFieldsAreIgnored()
    {
        ConfigCamera config = JsonConvert.DeserializeObject<ConfigCamera>("{\"Code\":\"camera\",\"UsingFileCaching\":false,\"IsCVCIEFileSave\":false}")!;
        JObject serialized = JObject.FromObject(config);

        Assert.Equal("camera", config.Code);
        Assert.Null(serialized.Property("UsingFileCaching"));
        Assert.Null(serialized.Property("IsCVCIEFileSave"));
    }

    [Theory]
    [InlineData("", false, "")]
    [InlineData("SV6100_F3.6_ND0_White255", true, "_SV6100_F3.6_ND0_White255")]
    [InlineData("SelectedButUnused", false, "")]
    [InlineData("Color/校正:*?", true, "_Color_校正___")]
    public void SavingKeepsRawFileAndInMemoryCieWithoutWritingCieFile(string calibrationTemplate, bool calibrationApplied, string expectedSuffix)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionCameraTests", Guid.NewGuid().ToString("N")));
        try
        {
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 3, Height = 2, Channels = 1, SourceBpp = 8, CieBpp = 32,
                DeviceCode = "camera", Exposure = [10], PrimaryBufferKind = LocalFrameBufferKind.CvCie,
                CalibrationTemplate = calibrationTemplate, IsMirrorReady = calibrationApplied
            }, 6, 24);
            LocalFrameFileService.SaveCapture(frame, LocalFrameFileService.CreateCapturePath(root, "camera", frame.Metadata));
            string fileName = Path.GetFileName(frame.CvRawFilePath);
            Assert.Matches(@"^\d{8}_\d{6}_\d{3}" + System.Text.RegularExpressions.Regex.Escape(expectedSuffix) + @"(?:_\d+)?\.cvraw$", fileName);
            Assert.Equal(Path.Combine(root, "camera", "Data"), Path.GetDirectoryName(Path.GetDirectoryName(frame.CvRawFilePath)));
            Assert.True(File.Exists(frame.CvRawFilePath));
            Assert.Empty(frame.CvCieFilePath);
            Assert.Empty(Directory.EnumerateFiles(root, "*.cvcie", SearchOption.AllDirectories));
            Assert.True(frame.HasCie);
            var model = LocalCameraResultService.CreateModel(1, -1, frame, new LocalCameraCaptureResult { Frame = frame }, null, null, false);
            Assert.Equal(frame.CvRawFilePath, model.FileUrl);
        }
        finally
        {
            string expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionCameraTests")) + Path.DirectorySeparatorChar;
            Assert.StartsWith(expectedParent, root, StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CapturePathsAreDistinctBeforeEitherFileIsSaved()
    {
        string root = Path.Combine(Path.GetTempPath(), "ColorVisionCameraTests", Guid.NewGuid().ToString("N"));
        string first = LocalFrameFileService.CreateCapturePath(root, "camera");
        string second = LocalFrameFileService.CreateCapturePath(root, "camera");
        Assert.NotEqual(first, second);
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
    }

    [Fact]
    public void StreamedRawSaveMatchesLegacyCvrawBytes()
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionCameraTests"));
        string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        string legacyFile = Path.Combine(root, "legacy.cvraw");
        byte[] pixels = [1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0];
        float[] exposure = [1.25f, 2.5f, 3.75f];
        try
        {
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 2,
                Height = 1,
                Channels = 3,
                SourceBpp = 16,
                Gain = 4.5f,
                Exposure = exposure,
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
            }, pixels.Length, 0);
            using (var lease = frame.Acquire())
                Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length);

            LocalFrameFileService.SaveCapture(frame, LocalFrameFileService.CreateCapturePath(root, "camera"));
            using var legacy = new CVCIEFile
            {
                Version = 1,
                FileExtType = CVType.Raw,
                Rows = 1,
                Cols = 2,
                Bpp = 16,
                Channels = 3,
                Gain = 4.5f,
                Exp = exposure,
                SrcFileName = string.Empty,
                Data = pixels,
            };
            Assert.True(CVFileUtil.WriteCVRaw(legacyFile, legacy));

            Assert.Equal(File.ReadAllBytes(legacyFile), File.ReadAllBytes(frame.CvRawFilePath));
        }
        finally
        {
            Assert.StartsWith(parent + Path.DirectorySeparatorChar, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreviewOwnsPixelsAfterDownstreamMutationAndFrameDisposal(bool withCie)
    {
        StaTest.Run(() =>
        {
            using var frame = CreateFrame(withCie);
            using (var lease = frame.Acquire())
            {
                Marshal.Copy(new byte[] { 1, 2, 3, 4, 5, 6 }, 0, lease.RawPointer, 6);
                if (withCie) Marshal.Copy(new float[] { 1, 2, 3, 4, 5, 6 }, 0, lease.CiePointer, 6);
            }
            var preview = LocalCameraPreview.Create(frame);
            using (var lease = frame.Acquire())
            {
                Marshal.Copy(new byte[6], 0, lease.RawPointer, 6);
                if (withCie) Marshal.Copy(new byte[24], 0, lease.CiePointer, 24);
            }
            frame.Metadata.Exposure[0] = 99;
            frame.Dispose();
            Assert.Equal(10, preview.Exposure[0]);
            byte[] pixels = new byte[6];
            preview.Bitmap.CopyPixels(pixels, 3, 0);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, pixels);
            Assert.True(preview.Bitmap.IsFrozen);
            Assert.Equal(withCie ? 24 : 0, preview.CieData.Length);
            if (withCie) Assert.Equal(1f, BitConverter.ToSingle(preview.CieData, 0));
        });
    }

    [Fact]
    public void PreviewMirrorsPendingRawWithoutMutatingFlowOrFilePixels()
    {
        StaTest.Run(() =>
        {
            using var frame = CreateFrame(false, CVImageFlipMode.Y);
            using var lease = frame.Acquire();
            byte[] original = [1, 2, 3, 4, 5, 6];
            Marshal.Copy(original, 0, lease.RawPointer, original.Length);
            var preview = LocalCameraPreview.Create(frame);
            byte[] pixels = new byte[6];
            preview.Bitmap.CopyPixels(pixels, 3, 0);
            Assert.Equal(new byte[] { 3, 2, 1, 6, 5, 4 }, pixels);
            Assert.Equal(original, lease.CopyRawToArray());
            Assert.False(frame.IsRawFlipApplied);
        });
    }

    [Fact]
    public void PreviewHandlesOddWidthRgb48RowsWithoutPaddingCorruption()
    {
        StaTest.Run(() =>
        {
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 1, Height = 2, Channels = 3, SourceBpp = 16 }, 12, 0);
            using var lease = frame.Acquire();
            Marshal.Copy(new short[] { 10, 20, 30, 40, 50, 60 }, 0, lease.RawPointer, 6);
            var preview = LocalCameraPreview.Create(frame);
            short[] pixels = new short[6];
            preview.Bitmap.CopyPixels(pixels, 6, 0);
            // Packed BGR16 source becomes RGB48 for WPF; green stays in the middle.
            Assert.Equal(new short[] { 30, 20, 10, 60, 50, 40 }, pixels);
            Assert.Equal(new byte[] { 10, 0, 20, 0, 30, 0, 40, 0, 50, 0, 60, 0 }, lease.CopyRawToArray());
        });
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 3)]
    [InlineData(16, 1)]
    [InlineData(16, 3)]
    public void RawPreviewMatchesCvrawDecoderWithoutChangingSourceSamples(int bpp, int channels)
    {
        StaTest.Run(() =>
        {
            const int width = 3, height = 2;
            int stride = width * channels * (bpp / 8);
            byte[] raw = Enumerable.Range(0, stride * height).Select(i => (byte)(i * 7)).ToArray();
            byte[] original = (byte[])raw.Clone();
            using var file = new CVCIEFile { Cols = width, Rows = height, Channels = channels, Bpp = bpp, FileExtType = CVType.Raw, Data = raw };
            using var mat = file.ToMat(showErrors: false);
            var decoded = mat.ToWriteableBitmap();
            var preview = LocalCameraPreview.CreateRawBitmap(raw, bpp, channels, width, height);
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = width, Height = height, SourceBpp = bpp, Channels = channels
            }, raw.Length, 0);
            using var lease = frame.Acquire();
            Marshal.Copy(raw, 0, lease.RawPointer, raw.Length);
            var framePreview = LocalCameraPreview.Create(frame).Bitmap;
            byte[] expected = new byte[raw.Length], actual = new byte[raw.Length];
            decoded.CopyPixels(expected, stride, 0);
            preview.CopyPixels(actual, stride, 0);
            Assert.Equal(decoded.Format, preview.Format);
            Assert.Equal(expected, actual);
            framePreview.CopyPixels(actual, stride, 0);
            Assert.Equal(decoded.Format, framePreview.Format);
            Assert.Equal(expected, actual);
            Assert.Equal(original, lease.CopyRawToArray());
            Assert.Equal(original, raw);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(7)]
    public void PreviewRejectsRawBufferThatDoesNotMatchMetadata(int rawLength)
    {
        StaTest.Run(() =>
        {
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 2, Height = 1, SourceBpp = 8, Channels = 3
            }, rawLength, 24);
            Assert.Throws<InvalidOperationException>(() => LocalCameraPreview.Create(frame));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResultRecordKeepsMemoryOnlyAndSavedFileSemantics(bool saveFiles)
    {
        using var frame = CreateFrame(true);
        if (saveFiles)
        {
            frame.CvRawFilePath = @"C:\captures\Local.cvraw";
            frame.CvCieFilePath = @"C:\captures\Local.cvcie";
        }
        var capture = new LocalCameraCaptureResult { Frame = frame, TotalTimeMs = 42 };
        var model = LocalCameraResultService.CreateModel(123, 2, frame, capture, null, null, false);
        Assert.Equal(123, model.BatchId);
        Assert.Equal("camera", model.DeviceCode);
        Assert.Equal(42, model.TotalTime);
        Assert.Equal(0, model.ResultCode);
        Assert.Equal(saveFiles ? @"C:\captures\Local.cvcie" : null, model.FileUrl);
        Assert.Equal(saveFiles ? "Local.cvraw" : null, model.RawFile);
        Assert.Equal(saveFiles ? (sbyte?)1 : null, model.FileType);
        Assert.True(JObject.Parse(model.ImgFrameInfo!).Value<bool>("hasCie"));
        Assert.Equal(10f, JObject.Parse(model.Params!)["ExpTime"]![0]!.Value<float>());
    }

    [Fact]
    public void AutoExposureWritesAllChannelsAndRejectsInvalidNativeResultAtomically()
    {
        CameraRunParam parameters = new();
        LocalCameraAutoExposure.ApplyExposure(parameters, [12, 23, 34], true);
        var display = new DisplayCameraConfig();
        LocalCameraAutoExposure.ApplyDisplayExposure(display, parameters, [0.1f, 0.2f, 0.3f]);
        Assert.Equal(12, display.ExpTime);
        Assert.Equal(12, display.ExpTimeR);
        Assert.Equal(23, display.ExpTimeG);
        Assert.Equal(34, display.ExpTimeB);
        Assert.Throws<InvalidOperationException>(() => LocalCameraAutoExposure.ApplyExposure(parameters, [1, float.NaN, 3], true));
        Assert.Equal(23, parameters.ExpTimeG);
        LocalCameraAutoExposure.ApplyExposure(parameters, [8], false);
        Assert.Equal(8, parameters.ExpTimeB);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutoExposureFrameCarriesItsActualExposureAndOwnsPreviewAfterDisposal(bool threeChannels)
    {
        LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = 3, Height = 2, SourceBpp = 8, Channels = 3,
            DeviceCode = "camera", Gain = 2, FlipMode = CVImageFlipMode.Y,
            PrimaryBufferKind = LocalFrameBufferKind.CvRaw, IsMirrorReady = true
        }, 18, 0);
        CameraRunParam parameters = new();
        try
        {
            using (var lease = frame.Acquire()) Marshal.Copy(Enumerable.Repeat((byte)64, 18).ToArray(), 0, lease.RawPointer, 18);
            LocalCameraAutoExposure.ApplyFrameResult(frame, parameters, [12, 23, 34], [70, 71, 72], threeChannels);
            Assert.Equal(threeChannels ? new float[] { 12, 23, 34 } : new float[] { 12, 12, 12 }, frame.Metadata.Exposure);
            Assert.Equal(2, frame.Metadata.Gain);
            Assert.Equal(CVImageFlipMode.Y, frame.Metadata.FlipMode);
            var preview = LocalCameraPreview.Create(frame);
            frame.Dispose();
            Assert.True(preview.Bitmap.IsFrozen);
            Assert.Equal(frame.Metadata.Exposure, preview.Exposure);
            byte[] pixels = new byte[18];
            preview.Bitmap.CopyPixels(pixels, 9, 0);
            Assert.All(pixels, value => Assert.Equal(64, value));
        }
        finally { frame.Dispose(); }
    }

    [Fact]
    public void InvalidAutoExposureFrameResultDoesNotUpdateExposureOrMetadata()
    {
        using LocalFlowFrame frame = CreateFrame(false);
        CameraRunParam parameters = new();
        parameters.SetAllExposure(10);
        Assert.Throws<InvalidOperationException>(() => LocalCameraAutoExposure.ApplyFrameResult(frame, parameters, [12, 23, 34], [float.NaN, 70, 70], false));
        Assert.Equal(10, parameters.ExpTime);
        Assert.Equal(new float[] { 10 }, frame.Metadata.Exposure);
        Assert.Throws<InvalidOperationException>(() => LocalCameraAutoExposure.ApplyFrameResult(frame, parameters, [float.NaN, 23, 34], [70, 70, 70], false));
        Assert.Equal(10, parameters.ExpTime);
        Assert.Equal(new float[] { 10 }, frame.Metadata.Exposure);
    }

    private static LocalFlowFrame CreateFrame(bool cie, CVImageFlipMode flip = CVImageFlipMode.None) => LocalFlowFrame.Allocate(new LocalFrameMetadata
    {
        Width = 3, Height = 2, Channels = 1, SourceBpp = 8, CieBpp = 32,
        DeviceCode = "camera", Exposure = [10], FlipMode = flip,
        PrimaryBufferKind = cie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw
    }, 6, cie ? 24 : 0);
}
