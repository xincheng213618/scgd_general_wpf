using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using FlowEngineLib.Algorithm;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;
using System.IO;

namespace ColorVision.UI.Tests;

public class LocalCameraResultTests
{
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
            Assert.Equal(new short[] { 10, 30, 20, 40, 60, 50 }, pixels);
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
