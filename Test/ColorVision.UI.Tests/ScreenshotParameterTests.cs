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
    [Theory]
    [InlineData(192, 48, 1)]
    [InlineData(48, 192, 2)]
    public async Task BackgroundScreenshotPreservesImageEdgesAndOverlayAlignmentAtNonDefaultDpi(double dpiX, double dpiY, int divisor)
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-shot-dpi-{Guid.NewGuid():N}.png");
        ImageView view = WpfTestHost.Invoke(CreateView);
        try
        {
            await PublishDpiFixture(view, dpiX, dpiY);
            var snapshot = WpfTestHost.Invoke(() => Assert.IsType<ImageViewSnapshot>(view.CaptureSnapshotForBackgroundSave()));
            await ImageView.SaveSnapshotWithOptionsAsync(snapshot, path, new() { ScaleDivisor = divisor });
            WpfTestHost.Invoke(() =>
            {
                using var stream = File.OpenRead(path);
                BitmapSource frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                AssertDpiFixture(frame, divisor, dpiX, dpiY, fromPng: true);
            });
        }
        finally { WpfTestHost.Invoke(view.Dispose); File.Delete(path); }
    }

    [Fact]
    public async Task ImmediateScreenshotUsesPhysicalPixelDimensionsAtNonDefaultDpi()
    {
        ImageView view = WpfTestHost.Invoke(CreateView);
        try
        {
            await PublishDpiFixture(view, 192, 48);
            WpfTestHost.Invoke(() => AssertDpiFixture(Assert.IsAssignableFrom<BitmapSource>(view.CaptureSnapshot()), 1, 192, 48, fromPng: false));
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    private static Task PublishDpiFixture(ImageView view, double dpiX, double dpiY) => WpfTestHost.Invoke(async () =>
    {
        byte[] pixels = new byte[8 * 8 * 4];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int index = (y * 8 + x) * 4;
                pixels[index + (x < 4 ? 2 : 0)] = 255;
                pixels[index + 3] = 255;
            }
        }
        BitmapSource source = BitmapSource.Create(8, 8, dpiX, dpiY, PixelFormats.Bgra32, null, pixels, 32);
        source.Freeze();
        view.Config.FilePath = "dpi-fixture.png";
        await view.PublishDecodedImageContentAsync(new(source, null, "dpi-fixture.png"), source);
        view.ImageShow.Measure(new Size(source.Width, source.Height));
        view.ImageShow.Arrange(new Rect(0, 0, source.Width, source.Height));
        var marker = new DrawingVisual();
        using (DrawingContext drawing = marker.RenderOpen())
            drawing.DrawRectangle(Brushes.Lime, null, new Rect(4 * 96 / dpiX, 4 * 96 / dpiY, 2 * 96 / dpiX, 2 * 96 / dpiY));
        view.ImageShow.AddVisual(marker);
    });

    private static void AssertDpiFixture(BitmapSource frame, int divisor, double dpiX, double dpiY, bool fromPng)
    {
        int size = 8 / divisor;
        Assert.Equal((size, size), (frame.PixelWidth, frame.PixelHeight));
        double dpiTolerance = fromPng ? 0.02 : 0.0; // PNG stores resolution as integer pixels per metre.
        Assert.InRange(frame.DpiX, dpiX - dpiTolerance, dpiX + dpiTolerance);
        Assert.InRange(frame.DpiY, dpiY - dpiTolerance, dpiY + dpiTolerance);
        BitmapSource converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[size * size * 4];
        converted.CopyPixels(pixels, size * 4, 0);

        Assert.Equal(Colors.Red, Read(0, size - 1));
        Assert.Equal(Colors.Blue, Read(size - 1, 0));
        Assert.Equal(Colors.Blue, Read(size - 1, size - 1));
        Assert.Equal(Colors.Lime, Read(4 / divisor, 4 / divisor));

        Color Read(int x, int y)
        {
            int index = (y * size + x) * 4;
            return Color.FromArgb(pixels[index + 3], pixels[index + 2], pixels[index + 1], pixels[index]);
        }
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotsDescribeTheVisibleImageAfterReplacementFilesFailToOpen(bool calibrated)
    {
        string prefix = Path.Combine(Path.GetTempPath(), $"cv-shot-open-{Guid.NewGuid():N}");
        string failedPath = prefix + "-missing.png";
        string replacementPath = prefix + "-replacement.png";
        string immediatePath = prefix + "-immediate.png";
        string backgroundPath = prefix + "-background.png";
        ImageView view = WpfTestHost.Invoke(CreateView);
        try
        {
            ColorVisionTiffParameters? parameters = calibrated ? Parameters() : null;
            if (parameters != null) parameters.InputFileName = "fixture.tiff";
            await Publish(view, parameters, dpi: 120);
            var original = WpfTestHost.Invoke(() => view.ViewBitmapSource);

            foreach (string path in new[] { failedPath, replacementPath })
                await Assert.ThrowsAsync<FileNotFoundException>(() => WpfTestHost.Invoke(() =>
                {
                    view.OpenImage(path);
                    return view.ImageOpenCompletion;
                }));

            var (immediate, background) = WpfTestHost.Invoke(() =>
            {
                Assert.Same(original, view.ViewBitmapSource);
                if (parameters != null)
                {
                    parameters.Acquisition!.Exposure[0] = 999;
                    parameters.CalibrationSummary!.Sources[0] = new() { FileName = "later.dat" };
                }
                view.ImageShow.Measure(new Size(4, 4));
                view.ImageShow.Arrange(new Rect(0, 0, 4, 4));
                return (Assert.IsAssignableFrom<BitmapSource>(view.CaptureSnapshot()),
                    Assert.IsType<ImageViewSnapshot>(view.CaptureSnapshotForBackgroundSave(includeOverlays: false)));
            });
            using (background)
            {
                await ImageView.SaveSnapshotAsync(immediate, immediatePath);
                await ImageView.SaveSnapshotAsync(background, backgroundPath);
            }
            WpfTestHost.Invoke(() =>
            {
                foreach (string path in new[] { immediatePath, backgroundPath })
                {
                    var record = ReadRecord(path);
                    AssertRecordOnly(record);
                    Assert.Equal("fixture.tiff", record.InputFileName);
                    Assert.Equal(4, record.Rows);
                    Assert.Equal(16, record.Bpp);
                    Assert.Equal(calibrated ? "SN-123" : null, record.Acquisition?.Camera?.CameraId);
                    if (calibrated)
                    {
                        Assert.Equal(10, record.Acquisition!.Exposure[0]);
                        Assert.Equal("原始.dat", Assert.Single(record.CalibrationSummary!.Sources).FileName);
                    }
                    else Assert.Null(record.CalibrationSummary);
                }
                Assert.Equal(120, immediate.DpiX);
                Assert.Equal(120, background.DpiY);
            });

            await WpfTestHost.Invoke(() =>
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2)));
                using (FileStream stream = File.Create(replacementPath)) encoder.Save(stream);
                view.OpenImage(replacementPath);
                return view.ImageOpenCompletion;
            });
            var recovered = WpfTestHost.Invoke(() => Assert.IsType<ImageViewSnapshot>(view.CaptureSnapshotForBackgroundSave(false)));
            using (recovered) await ImageView.SaveSnapshotAsync(recovered, backgroundPath);
            WpfTestHost.Invoke(() =>
            {
                var record = ReadRecord(backgroundPath);
                Assert.Equal(Path.GetFileName(replacementPath), record.InputFileName);
                Assert.Equal(2, record.Rows);
                Assert.Null(record.Acquisition);
                Assert.Null(record.CalibrationSummary);
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
            File.Delete(replacementPath);
            File.Delete(immediatePath);
            File.Delete(backgroundPath);
        }
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

    private static Task Publish(ImageView view, ColorVisionTiffParameters? parameters, double dpi = 96) => WpfTestHost.Invoke(() =>
    {
        BitmapSource pixels = BitmapSource.Create(4, 4, dpi, dpi, PixelFormats.Gray16, null, Enumerable.Repeat((ushort)10000, 16).ToArray(), 8);
        pixels.Freeze();
        if (parameters != null)
        {
            parameters.PixelLayout = pixels.Format.ToString();
            parameters.PixelSha256 = ColorVisionPixelBinding.ComputeSha256(pixels);
        }
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
