using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Tif;
using ColorVision.ImageEditor.Draw.Ruler;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

/// <summary>Screenshot records describe capture provenance, never replayable source samples.</summary>
public sealed class ScreenshotParameterTests
{
    [Fact]
    public async Task NativeCvRawOpenUsesPersistedCameraAndCalibrationSummaryForScreenshot()
    {
        string rawPath = Path.Combine(Path.GetTempPath(), $"cv-shot-{Guid.NewGuid():N}.cvraw");
        string pngPath = Path.ChangeExtension(rawPath, ".png");
        ImageView view = WpfTestHost.Invoke(CreateView);
        try
        {
            var parameters = Parameters();
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(4, 4, 16, 1);
            Assert.True(CVFileUtil.WriteCVRaw(rawPath, raw));
            parameters.Calibration!.ToObject<ColorCalibrationSnapshot>()!.Save(rawPath, true);
            CameraFileMetadata.Save(rawPath, CameraFileMetadata.AcquisitionKind, parameters.Acquisition);
            Task completion = WpfTestHost.Invoke(() =>
            {
                view.Config.SetImageMetadata(ImageViewPropertyKeys.CameraModel, "previous-camera");
                view.IEditorToolFactory.IImageOpens[".cvraw"] = new CVRawOpen(view.EditorContext);
                view.OpenImage(rawPath);
                return view.ImageOpenCompletion;
            });
            await completion;
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal("captured-model", view.Config.GetProperties<string>("ColorVision.Camera.Model"));
                Assert.Null(view.Config.GetProperties<string>(ImageViewPropertyKeys.CameraModel));
                Assert.Equal(ImageCalibrationConfig.DefaultKey, ImageCalibrationService.ResolveCalibrationKey(view.Config));
            });
            var snapshot = WpfTestHost.Invoke(() => Assert.IsType<ImageViewSnapshot>(view.CaptureSnapshotForBackgroundSave(false)));
            await ImageView.SaveSnapshotAsync(snapshot, pngPath);
            WpfTestHost.Invoke(() =>
            {
                var record = ReadRecord(pngPath);
                AssertRecordOnly(record);
                Assert.Equal("SN-123", record.Acquisition!.Camera!.CameraId);
                Assert.Equal("模板一🌈", record.CalibrationSummary!.Template);
                Assert.Equal("原始.dat", Assert.Single(record.CalibrationSummary.Sources).FileName);
            });
        }
        finally { WpfTestHost.Invoke(view.Dispose); File.Delete(rawPath); File.Delete(pngPath); }
    }

    [Theory]
    [InlineData(ImageViewSnapshotFormat.Png)]
    [InlineData(ImageViewSnapshotFormat.Jpeg)]
    public async Task BackgroundScreenshotKeepsCapturedRecordsAfterViewClosesAndCannotReplay(ImageViewSnapshotFormat format)
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-shot-{Guid.NewGuid():N}." + (format == ImageViewSnapshotFormat.Png ? "png" : "jpg"));
        ImageView view = WpfTestHost.Invoke(CreateView);
        ImageView? reopened = null;
        try
        {
            var parameters = Parameters();
            await Publish(view, parameters);
            ImageViewSnapshot snapshot = WpfTestHost.Invoke(() =>
            {
                Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
                var captured = Assert.IsType<ImageViewSnapshot>(view.CaptureSnapshotForBackgroundSave(false));
                parameters.Acquisition!.Exposure[0] = 999;
                parameters.CalibrationSummary!.Sources[0] = new() { FileName = "later.dat" };
                view.Dispose();
                return captured;
            });
            await ImageView.SaveSnapshotWithOptionsAsync(snapshot, path, new() { Format = format, ScaleDivisor = 2 });
            WpfTestHost.Invoke(() =>
            {
                var record = ReadRecord(path);
                AssertRecordOnly(record);
                Assert.Equal("相机一", record.Acquisition!.Camera!.DeviceName);
                Assert.Equal("SN-123", record.Acquisition.Camera.CameraId);
                Assert.Equal(10, record.Acquisition.Exposure[0]);
                Assert.Equal("原始.dat", Assert.Single(record.CalibrationSummary!.Sources).FileName);
                Assert.Equal("模板一🌈", record.CalibrationSummary.Template);
                Assert.Equal(4, record.Rows);
                Assert.Equal(2, record.RenderedOutput!.Width);
                Assert.Equal(2, record.RenderedOutput.Height);
                reopened = CreateView();
                reopened.Config.FilePath = path;
            });
            await WpfTestHost.Invoke(() => new CommonImageOpen(reopened!.EditorContext).OpenImageAsync(reopened.EditorContext, path));
            WpfTestHost.Invoke(() =>
            {
                Assert.NotNull(reopened!.ViewBitmapSource);
                Assert.False(reopened.Config.GetProperties<bool>("HasCieMeasurements"));
                Assert.False(reopened.Config.GetProperties<bool>("IsCVCIE"));
                Assert.Null(reopened.ImageContentCapability);
                Assert.NotNull(reopened.Config.GetProperties<string>("ColorVision.CalibrationSummary"));
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
            if (reopened != null) WpfTestHost.Invoke(reopened.Dispose);
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImmediateAndDetachedBitmapScreenshotSaveShareTheRecordOnlyContract()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-shot-{Guid.NewGuid():N}.png");
        ImageView view = WpfTestHost.Invoke(CreateView);
        try
        {
            await Publish(view, Parameters());
            BitmapSource snapshot = WpfTestHost.Invoke(() =>
            {
                view.ImageShow.Measure(new Size(4, 4));
                view.ImageShow.Arrange(new Rect(0, 0, 4, 4));
                view.Save(path);
                AssertRecordOnly(ReadRecord(path));
                return Assert.IsAssignableFrom<BitmapSource>(view.CaptureSnapshot());
            });
            WpfTestHost.Invoke(view.Clear);
            await ImageView.SaveSnapshotAsync(snapshot, path);
            WpfTestHost.Invoke(() => AssertRecordOnly(ReadRecord(path)));
        }
        finally { WpfTestHost.Invoke(view.Dispose); File.Delete(path); }
    }

    private static void AssertRecordOnly(ColorVisionTiffParameters record)
    {
        Assert.Equal("display", record.PixelRole);
        Assert.Equal("Snapshot", record.ExportedChannel);
        Assert.Null(record.Calibration);
        Assert.Null(record.PixelSha256);
        Assert.Null(record.PixelLayout);
        Assert.Null(record.MeasurementChannel);
        Assert.Equal(0, record.PixelChannels);
        Assert.DoesNotContain("Coefficients", record.ToJson(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanReplay", record.ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    private static ColorVisionTiffParameters ReadRecord(string path)
    {
        using var stream = File.OpenRead(path);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Assert.True(ColorVisionTiffParameters.TryRead(frame.Metadata as BitmapMetadata, out var record));
        return record!;
    }

    private static Task Publish(ImageView view, ColorVisionTiffParameters parameters) => WpfTestHost.Invoke(() =>
    {
        BitmapSource pixels = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Gray16, null, Enumerable.Repeat((ushort)10000, 16).ToArray(), 8);
        pixels.Freeze();
        parameters.PixelLayout = pixels.Format.ToString();
        parameters.PixelSha256 = ColorVisionPixelBinding.ComputeSha256(pixels);
        view.Config.FilePath = "fixture.tiff";
        return view.PublishDecodedImageContentAsync(new(pixels, parameters, "fixture.tiff"), pixels);
    });

    private static ColorVisionTiffParameters Parameters()
    {
        var summary = new ImageCalibrationSummary
        {
            Template = "模板一🌈", Sources = [new() { FileName = "原始.dat", FileSha256 = new string('A', 64) }]
        };
        var calibration = new ColorCalibrationSnapshot
        {
            Width = 4, Height = 4, RawBpp = 16, Channels = 1, TransformKind = 2,
            Coefficients = [2, 0, 0, 0, 0, 0, 0, 0, 0], Exposure = [10, 10, 10], Template = summary.Template!, Provenance = summary
        };
        return new()
        {
            SourceType = "CVRAW", Rows = 4, Cols = 4, Bpp = 16, SourceChannels = 1,
            PixelRole = "calibration-input", PixelChannels = 1, Calibration = JToken.FromObject(calibration),
            Acquisition = new()
            {
                Camera = new() { DeviceName = "相机一", CameraId = "SN-123", Model = "captured-model", Manufacturer = "captured-maker" }, Exposure = [10], ExposureUnit = "ms"
            },
            CalibrationSummary = summary
        };
    }

    private static ImageView CreateView()
    {
        Application app = Application.Current;
        app.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        app.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        app.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        app.Resources["ToolBarImage"] = new Style(typeof(Image));
        app.Resources["BaseStyle"] = new Style(typeof(Control));
        app.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        app.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
        ImageView view = new() { EnableEditorImageServices = false };
        view.IEditorToolFactory.IEditorTools.Clear();
        view.RegisterImageContentProvider(new CalibratedImageContentProvider());
        return view;
    }
}
