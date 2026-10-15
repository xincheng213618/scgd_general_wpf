using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Settings;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CameraPreviewFileHandoffTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"cv-preview-handoff-{Guid.NewGuid():N}");
    private readonly IConfigService previousConfig = ConfigService.Instance;
    private readonly ImageView view;
    private readonly CVRawOpen opener;
    private readonly CameraRealtimeFramePipeline pipeline;

    public CameraPreviewFileHandoffTests()
    {
        Directory.CreateDirectory(root);
        CVFileReadCache.Release();
        ConfigService.SetInstance(new ConfigHandler());
        (view, opener, pipeline) = WpfTestHost.Invoke(() =>
        {
            Application app = Application.Current;
            app.Resources["TextBox.Small"] = new Style(typeof(TextBox));
            app.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            app.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            app.Resources["ToolBarImage"] = new Style(typeof(Image));
            app.Resources["BaseStyle"] = new Style(typeof(Control));
            app.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            app.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            DefaultRealtimeCameraConfig.Current.IsCalArtculation = false;
            ViewCameraConfig.Instance.AutoRefreshView = false;
            CvcieDisplayConfig.Current.EnableTrueColor = false;
            ImageView created = new() { EnableEditorImageServices = false };
            created.IEditorToolFactory.IEditorTools.Clear();
            created.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
            CVRawOpen raw = new(created.EditorContext);
            created.IEditorToolFactory.IImageOpens[".cvraw"] = raw;
            created.IEditorToolFactory.IImageOpens[".cvcie"] = raw;
            return (created, raw, new CameraRealtimeFramePipeline());
        });
    }

    [Fact]
    public async Task VideoStartRetiresPendingFileOpenBeforeItCanOverwriteAFrame()
    {
        string path = WriteRaw("pending.cvraw", 13);
        SemaphoreSlim gate = OpenGate;
        await gate.WaitAsync();
        bool held = true;
        try
        {
            WpfTestHost.Invoke(() => { view.OpenImage(path); pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false); });
            await PresentAsync([71, 72, 73, 74, 75, 76], 8, 1);
            BitmapSource video = WpfTestHost.Invoke(() => (BitmapSource)view.ViewBitmapSource);
            gate.Release();
            held = false;
            await gate.WaitAsync();
            held = true;
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(video, view.ViewBitmapSource);
                Assert.Equal(new byte[] { 71, 72, 73, 74, 75, 76 }, Pixels(video));
                Assert.True(string.IsNullOrEmpty(view.Config.FilePath));
                Assert.Null(view.EditorContext.IImageOpen);
            });
        }
        finally { if (held) gate.Release(); }
    }

    [Fact]
    public async Task VideoStartDetachesOldFileCalibrationAndMeasurementTools()
    {
        string path = Path.Combine(root, "calibrated.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 2, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        ColorCalibrationSnapshot.Create(RawColorTransformV1.Create(), 3, 2, 16, raw.Exp, "previous-file").Save(path, true);
        await OpenAsync(path);
        PoiMeasurementBuffer previous = WpfTestHost.Invoke(() =>
        {
            Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
            return Measurement!;
        });
        WpfTestHost.Invoke(() => pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false));
        await PresentAsync([71, 72, 73, 74, 75, 76], 8, 1);
        WpfTestHost.Invoke(() =>
        {
            Assert.False(view.Config.GetProperties<bool>("HasCieMeasurements"));
            Assert.False(view.Config.GetProperties<bool>("IsCVCIE"));
            Assert.Null(view.EditorContext.IImageOpen);
            Assert.Null(Measurement);
            Assert.Empty(view.ComboBoxLayers.Items);
            Assert.Null(view.EditorContext.ProcessingContext.ProfileMeasurementSources);
        });
        Assert.Throws<ObjectDisposedException>(() => previous.RawSource!.CreateChannel(0));
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 3)]
    [InlineData(16, 1)]
    [InlineData(16, 3)]
    public async Task VideoPixelsStayIndependentOfProducerAndFileCacheAndStoppingPreservesNewFile(int bpp, int channels)
    {
        string path = WriteRaw("result.cvraw", 29);
        WpfTestHost.Invoke(() => pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false));
        long allocations = CVFileReadCache.GetSnapshot().AllocationCount;
        byte[] samples = Enumerable.Range(0, 6 * channels * (bpp / 8)).Select(i => (byte)(i * 5 + 3)).ToArray();
        byte[] expected = (byte[])samples.Clone();
        await PresentAsync(samples, bpp, channels, clearProducer: true);
        BitmapSource video = WpfTestHost.Invoke(() => (BitmapSource)view.ViewBitmapSource);
        Assert.True(video.IsFrozen);
        Assert.Equal(expected, Pixels(video));
        Assert.Equal(allocations, CVFileReadCache.GetSnapshot().AllocationCount);
        var released = await LocalCalibrationCacheService.ReleaseAllAsync(Array.Empty<DeviceCamera>());
        Assert.True(released.Succeeded, string.Join("; ", released.Errors));
        Assert.Equal(expected, Pixels(video));
        WpfTestHost.Invoke(() => pipeline.Stop(resetRealtime: true));
        await OpenAsync(path);
        WpfTestHost.Invoke(() =>
        {
            var file = Assert.IsType<WriteableBitmap>(view.ViewBitmapSource);
            Assert.NotSame(video, file);
            Assert.All(Pixels(file), value => Assert.Equal(29, value));
            pipeline.Stop(resetRealtime: true, clearImageSource: true);
            Assert.Same(file, view.Presentation.DisplaySource);
        });
        Assert.Equal(expected, Pixels(video));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectCaptureSnapshotSurvivesLateFileReadCacheReleaseAndFrameDisposal(bool withCie)
    {
        await OpenAsync(WriteRaw("initial.cvraw", 11));
        string late = WriteRaw("late.cvraw", 29);
        SemaphoreSlim gate = OpenGate;
        await gate.WaitAsync();
        bool held = true;
        try
        {
            WpfTestHost.Invoke(() => view.OpenImage(late));
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 3, Height = 2, Channels = 1, SourceBpp = 8, CieBpp = 32, Exposure = [10],
                PrimaryBufferKind = withCie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw
            }, 6, withCie ? 24 : 0);
            byte[] raw = [51, 52, 53, 54, 55, 56];
            float[] cie = [1, 2, 3, 4, 5, 6];
            using (LocalFlowFrameLease lease = frame.Acquire())
            {
                Marshal.Copy(raw, 0, lease.RawPointer, raw.Length);
                if (withCie) Marshal.Copy(cie, 0, lease.CiePointer, cie.Length);
            }
            LocalCameraPreview preview = WpfTestHost.Invoke(() => LocalCameraPreview.Create(frame));
            using (LocalFlowFrameLease lease = frame.Acquire())
            {
                Marshal.Copy(new byte[6], 0, lease.RawPointer, 6);
                if (withCie) Marshal.Copy(new byte[24], 0, lease.CiePointer, 24);
            }
            frame.Dispose();
            WpfTestHost.Invoke(() => preview.Show(view));
            WriteRaw("other.cvraw", 99);
            CVFileReadCache.Release();
            gate.Release();
            held = false;
            await gate.WaitAsync();
            held = true;
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(raw, Pixels((BitmapSource)view.ViewBitmapSource));
                Assert.True(string.IsNullOrEmpty(view.Config.FilePath));
                if (withCie)
                {
                    Assert.Same(opener, view.EditorContext.IImageOpen);
                    Assert.True(view.Config.GetProperties<bool>("IsCVCIE"));
                    float[] measured = Measurement!.Borrow((pointer, _) =>
                    {
                        float[] copy = new float[6];
                        Marshal.Copy(pointer, copy, 0, copy.Length);
                        return copy;
                    });
                    Assert.Equal(cie, measured);
                }
                else Assert.Null(Measurement);
            });
            gate.Release();
            held = false;
            await OpenAsync(late);
            WpfTestHost.Invoke(() => Assert.All(Pixels((BitmapSource)view.ViewBitmapSource), value => Assert.Equal(29, value)));
            Assert.Equal(raw, Pixels(preview.Bitmap));
        }
        finally { if (held) gate.Release(); }
    }

    [Fact]
    public async Task VideoRestartDiscardsQueuedFrameBeforeNewSessionPublishes()
    {
        WriteableBitmap previous = WpfTestHost.Invoke(() =>
        {
            WriteableBitmap bitmap = new(3, 2, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 3, 2), new byte[] { 41, 42, 43, 44, 45, 46 }, 3, 0);
            view.OpenImage(bitmap);
            pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false);
            pipeline.SubmitFrame(new byte[] { 11, 12, 13, 14, 15, 16 }, 6, 3, 2, 1, 8, 3);
            // The first session's Background presentation is queued, but has not run on this dispatcher yet.
            pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false);
            return bitmap;
        });
        await view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task.WaitAsync(TimeSpan.FromSeconds(10));
        WpfTestHost.Invoke(() =>
        {
            Assert.Same(previous, view.ViewBitmapSource);
            Assert.False(view.EditorContext.ProcessingContext.StreamPresentation.IsActive);
        });
        await PresentAsync([71, 72, 73, 74, 75, 76], 8, 1);
        WpfTestHost.Invoke(() => Assert.Equal(new byte[] { 71, 72, 73, 74, 75, 76 }, Pixels((BitmapSource)view.ViewBitmapSource)));
    }

    private SemaphoreSlim OpenGate => (SemaphoreSlim)typeof(CVRawOpen).GetField("_rawOpenGate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(opener)!;
    private PoiMeasurementBuffer? Measurement => (PoiMeasurementBuffer?)typeof(CVRawOpen).GetField("_measurementBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(opener);
    private string WriteRaw(string name, byte value)
    {
        string path = Path.Combine(root, name);
        using CVCIEFile raw = new() { Version = 1, Cols = 3, Rows = 2, Bpp = 8, Channels = 1, Exp = [10], Data = Enumerable.Repeat(value, 6).ToArray() };
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        return path;
    }
    private async Task PresentAsync(byte[] samples, int bpp, int channels, bool clearProducer = false)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFrame(Guid id, WriteableBitmap source) => completion.TrySetResult();
        WpfTestHost.Invoke(() =>
        {
            view.EditorContext.ProcessingContext.StreamPresentation.FramePresented += OnFrame;
            pipeline.SubmitFrame(samples, samples.Length, 3, 2, channels, bpp, 3 * channels * (bpp / 8));
            if (clearProducer) Array.Clear(samples);
        });
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext.StreamPresentation.FramePresented -= OnFrame); }
    }
    private async Task OpenAsync(string path)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object? sender, ImageViewImageSourceLoadedEventArgs args) => completion.TrySetResult();
        WpfTestHost.Invoke(() => { view.ImageSourceLoaded += OnLoaded; view.OpenImage(path); });
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { WpfTestHost.Invoke(() => view.ImageSourceLoaded -= OnLoaded); }
    }
    private static byte[] Pixels(BitmapSource source)
    {
        int stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
        byte[] pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return pixels;
    }
    public void Dispose()
    {
        WpfTestHost.Invoke(() => { pipeline.Dispose(); view.Dispose(); });
        ConfigService.SetInstance(previousConfig);
        CVFileReadCache.Release();
        foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
        Directory.Delete(root);
    }
}
