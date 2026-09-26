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
    [InlineData(false)]
    [InlineData(true)]
    public void SavingCanKeepRawFileAndInMemoryCieWithoutWritingCieFile(bool includeCie)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionCameraTests", Guid.NewGuid().ToString("N")));
        try
        {
            using var frame = CreateFrame(true);
            LocalFrameFileService.SaveCapture(frame, root, "camera", includeCie: includeCie);
            Assert.True(File.Exists(frame.CvRawFilePath));
            Assert.Equal(includeCie, File.Exists(frame.CvCieFilePath));
            Assert.True(frame.HasCie);
            var model = LocalCameraResultService.CreateModel(1, -1, frame, new LocalCameraCaptureResult { Frame = frame }, null, null, false);
            Assert.Equal(includeCie ? frame.CvCieFilePath : frame.CvRawFilePath, model.FileUrl);
        }
        finally
        {
            string expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionCameraTests")) + Path.DirectorySeparatorChar;
            Assert.StartsWith(expectedParent, root, StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
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

            LocalFrameFileService.SaveCapture(frame, root, "camera", includeCie: false);
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
            byte[] expected = new byte[raw.Length], actual = new byte[raw.Length];
            decoded.CopyPixels(expected, stride, 0);
            preview.CopyPixels(actual, stride, 0);
            Assert.Equal(decoded.Format, preview.Format);
            Assert.Equal(expected, actual);
            Assert.Equal(original, raw);
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

    private static LocalFlowFrame CreateFrame(bool cie, CVImageFlipMode flip = CVImageFlipMode.None) => LocalFlowFrame.Allocate(new LocalFrameMetadata
    {
        Width = 3, Height = 2, Channels = 1, SourceBpp = 8, CieBpp = 32,
        DeviceCode = "camera", Exposure = [10], FlipMode = flip,
        PrimaryBufferKind = cie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw
    }, 6, cie ? 24 : 0);
}
