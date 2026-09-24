using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CvRawPixelBufferTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"cvraw-pixels-{Guid.NewGuid():N}");
    public CvRawPixelBufferTests() { Directory.CreateDirectory(root); CVFileReadCache.Release(); }

    [Fact]
    public void PathsAndMetadataReusePixelsButEveryLayoutChangeReplacesThem()
    {
        CvRawPixelBuffer buffer = new();
        byte[]? previous = null;
        // The first five layouts have the same byte count. Length alone cannot establish compatibility.
        foreach (var shape in new[] { (12, 4, 8, 1), (6, 8, 8, 1), (4, 4, 8, 3), (4, 2, 16, 3), (3, 2, 16, 4), (3, 2, 8, 4) })
        {
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(shape.Item1, shape.Item2, shape.Item3, shape.Item4);
            string firstPath = Path.Combine(root, "first.cvraw"), nextPath = Path.Combine(root, "next.CVRAW");
            Assert.True(CVFileUtil.WriteCVRaw(firstPath, raw));
            using CVCIEFile first = buffer.Read(firstPath);
            Assert.NotSame(previous, first.Data);
            Assert.Equal(raw.Data, first.Data);
            raw.Data[0] ^= 255;
            Assert.True(CVFileUtil.WriteCVRaw(nextPath, raw));
            CVFileMetadata.SetProperty(nextPath, "poi", 1, [1, 2, 3]);
            using CVCIEFile next = buffer.Read(nextPath);
            Assert.Same(first.Data, next.Data);
            Assert.Equal(raw.Data, next.Data);
            Assert.Equal(CVType.Raw, next.FileExtType);
            Assert.Equal(nextPath, next.FilePath);
            CVFileMetadata.SetProperty(nextPath, "poi", 1, [4, 5]);
            using CVCIEFile updated = buffer.Read(nextPath);
            Assert.Same(next.Data, updated.Data);
            Assert.Equal(raw.Data, updated.Data);
            previous = updated.Data;
        }
        buffer.Clear();
        using CVCIEFile afterClear = buffer.Read(Path.Combine(root, "next.CVRAW"));
        Assert.NotSame(previous, afterClear.Data);
    }

    [Fact]
    public void TruncatedOrMismatchedPayloadDoesNotReplaceOrPartiallyOverwriteExistingPixels()
    {
        CvRawPixelBuffer buffer = new();
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(13, 9, 16, 3);
        string good = Path.Combine(root, "good.cvraw");
        Assert.True(CVFileUtil.WriteCVRaw(good, raw));
        using CVCIEFile first = buffer.Read(good);
        string bad = Path.Combine(root, "bad.cvraw");
        File.Copy(good, bad);
        using (FileStream file = new(bad, FileMode.Open, FileAccess.Write)) file.SetLength(file.Length - 1);
        Assert.Throws<InvalidDataException>(() => buffer.Read(bad));
        Assert.Equal(raw.Data, first.Data);
        File.Copy(good, bad, overwrite: true);
        int offset = CVFileUtil.ReadCIEFileHeader(bad, out CVCIEFile header);
        using (header)
        using (FileStream file = new(bad, FileMode.Open, FileAccess.Write))
        using (BinaryWriter writer = new(file)) { file.Position = offset; writer.Write((long)raw.Data.Length - 2); }
        Assert.Throws<InvalidDataException>(() => buffer.Read(bad));
        using CVCIEFile again = buffer.Read(good);
        Assert.Same(first.Data, again.Data);
        Assert.Equal(raw.Data, again.Data);
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 3)]
    [InlineData(8, 4)]
    [InlineData(16, 1)]
    [InlineData(16, 3)]
    public async Task RepeatedOpenReusesInputAndDisplayWithoutChangingChannelOrder(int bpp, int channels)
    {
        ImageView view = CreateView();
        try
        {
            byte[]? input = null;
            WriteableBitmap? display = null;
            for (int pass = 0; pass < 3; pass++)
            {
                using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(7, 5, bpp, channels);
                for (int i = 0; i < raw.Data.Length; i++) raw.Data[i] ^= (byte)(pass * 37);
                string path = Path.Combine(root, $"frame-{pass}.cvraw");
                Assert.True(CVFileUtil.WriteCVRaw(path, raw));
                if (pass == 2) WpfTestHost.Invoke(() => display!.Freeze());
                await DisplayAsync(view, () => view.OpenImage(path));
                WpfTestHost.Invoke(() =>
                {
                    byte[] currentInput = Pixels(view)!;
                    WriteableBitmap currentDisplay = Assert.IsType<WriteableBitmap>(view.ViewBitmapSource);
                    if (pass > 0) Assert.Same(input, currentInput);
                    if (pass == 1) Assert.Same(display, currentDisplay);
                    if (pass == 2) Assert.NotSame(display, currentDisplay);
                    input = currentInput;
                    display = currentDisplay;
                    byte[] actual = new byte[raw.Data.Length];
                    currentDisplay.CopyPixels(actual, 7 * channels * (bpp / 8), 0);
                    byte[] expected = (byte[])raw.Data.Clone();
                    if (bpp == 16 && channels >= 3)
                        for (int pixel = 0; pixel < expected.Length; pixel += channels * 2)
                            for (int b = 0; b < 2; b++)
                                (expected[pixel + b], expected[pixel + 4 + b]) = (expected[pixel + 4 + b], expected[pixel + b]);
                    Assert.Equal(expected, actual);
                    Assert.Equal(raw.Data, currentInput);
                });
            }
            WpfTestHost.Invoke(view.Clear);
            SemaphoreSlim gate = OpenGate(view);
            await gate.WaitAsync();
            try { Assert.Null(Pixels(view)); }
            finally { gate.Release(); }
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [Fact]
    public async Task QueuedOpensPublishOnlyLatestRequestAndClearCancelsPendingOpen()
    {
        ImageView view = CreateView();
        try
        {
            string first = Path.Combine(root, "a.cvraw"), second = Path.Combine(root, "b.cvraw");
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(7, 5, 8, 1);
            Array.Fill(raw.Data, (byte)31);
            Assert.True(CVFileUtil.WriteCVRaw(first, raw));
            Array.Fill(raw.Data, (byte)99);
            Assert.True(CVFileUtil.WriteCVRaw(second, raw));
            SemaphoreSlim gate = OpenGate(view);
            await gate.WaitAsync();
            int loaded = 0;
            void OnLoaded(object? sender, ImageViewImageSourceLoadedEventArgs args) => loaded++;
            WpfTestHost.Invoke(() => view.ImageSourceLoaded += OnLoaded);
            try
            {
                await DisplayAsync(view, () =>
                {
                    view.OpenImage(first);
                    view.OpenImage(second);
                    view.OpenImage(first);
                    gate.Release();
                });
                await gate.WaitAsync();
                WpfTestHost.Invoke(() =>
                {
                    Assert.Equal(1, loaded);
                    Assert.All(Pixels(view)!, pixel => Assert.Equal(31, pixel));
                    view.OpenImage(second);
                    view.Clear();
                });
                gate.Release();
                await gate.WaitAsync();
                try
                {
                    WpfTestHost.Invoke(() => { Assert.Equal(1, loaded); Assert.Null(view.ViewBitmapSource); });
                    Assert.Null(Pixels(view));
                }
                finally { gate.Release(); }
            }
            finally { WpfTestHost.Invoke(() => view.ImageSourceLoaded -= OnLoaded); }
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [Fact]
    public async Task ReusedRawReadsNewCalibrationMetadataAndKeepsPoiAndProfileSources()
    {
        ImageView view = CreateView();
        try
        {
            string path = Path.Combine(root, "calibrated.cvraw");
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(9, 7, 16, 3);
            Assert.True(CVFileUtil.WriteCVRaw(path, raw));
            byte[]? input = null;
            PoiMeasurementBuffer? previous = null;
            for (int pass = 1; pass <= 2; pass++)
            {
                RawColorTransformV1 transform = RawColorTransformV1.Create();
                transform.Coefficients = [pass, 0, 0, 0, pass, 0, 0, 0, pass];
                ColorCalibrationSnapshot snapshot = ColorCalibrationSnapshot.Create(transform, 9, 7, 16, raw.Exp, "reuse");
                snapshot.Save(path, true);
                await DisplayAsync(view, () => { if (pass == 1) view.OpenImage(path); else Assert.True(view.RestoreOriginalImage()); });
                PoiMeasurementBuffer current = WpfTestHost.Invoke(() =>
                {
                    Assert.NotEmpty(view.EditorContext.ProcessingContext.ProfileMeasurementSources!);
                    Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
                    if (input != null) Assert.Same(input, Pixels(view));
                    input = Pixels(view);
                    return (PoiMeasurementBuffer)Field<CVRawOpen>("_measurementBuffer").GetValue(Opener(view))!;
                });
                using PoiMeasurementBuffer expected = new(raw, snapshot);
                PoiMeasurementPoint[] points = [new(3, 2, 1, 1, PoiMeasurementShape.Point)];
                Assert.Equal(PoiMeasurementService.CalculateRaw(expected, points), PoiMeasurementService.CalculateRaw(current, points));
                if (previous != null) Assert.Throws<ObjectDisposedException>(() => previous.RawSource!.CreateChannel(0));
                previous = current;
            }
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [Fact]
    public async Task RawLayerReadFinishesBeforeItsPixelsCanBeRetired()
    {
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(9, 7, 16, 3);
        ColorCalibrationSnapshot snapshot = ColorCalibrationSnapshot.Create(RawColorTransformV1.Create(), 9, 7, 16, raw.Exp, "borrow");
        using RawColorMeasurementSource source = new(raw, snapshot);
        using ManualResetEventSlim entered = new(), finish = new(), disposing = new();
        Task read = Task.Run(() => source.BorrowRaw(file =>
        {
            entered.Set();
            Assert.True(finish.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(raw.Data, file.Data);
            return 0;
        }));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        Thread dispose = new(() => { disposing.Set(); source.Dispose(); }) { IsBackground = true };
        dispose.Start();
        try
        {
            Assert.True(disposing.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(SpinWait.SpinUntil(() => (dispose.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(10)));
            Assert.True(dispose.IsAlive);
        }
        finally { finish.Set(); }
        await read;
        Assert.True(dispose.Join(TimeSpan.FromSeconds(10)));
        Assert.Throws<ObjectDisposedException>(() => source.BorrowRaw(file => file.Data.Length));
    }

    [CvRawSamplesFact]
    public void RealFilesReuseTheInputArrayWithNoFullFrameManagedAllocationOnRepeatedReads()
    {
        string samples = Environment.GetEnvironmentVariable("COLORVISION_CVRAW_SAMPLE_DIR")!;
        var paths = Directory.EnumerateFiles(samples, "*.cvraw").Select(path =>
        {
            Assert.True(CVFileUtil.ReadCIEFileHeader(path, out CVCIEFile meta) > 0);
            using (meta) return (Path: path, meta.Channels, meta.Bpp, Large: new FileInfo(path).Length > 200_000_000);
        }).GroupBy(file => (file.Channels, file.Bpp, file.Large)).Select(group => group.First().Path).ToArray();
        Assert.True(paths.Length >= 3);
        List<object> results = [];
        CvRawPixelBuffer buffer = new();
        foreach (string path in paths)
        {
            Assert.True(CVFileUtil.Read(path, out CVCIEFile expected));
            using (expected)
            using (CVCIEFile first = buffer.Read(path))
            {
                byte[] expectedHash = SHA256.HashData(expected.Data);
                Assert.Equal(expectedHash, SHA256.HashData(first.Data));
                const int repeats = 10;
                long before = GC.GetAllocatedBytesForCurrentThread();
                Stopwatch timer = Stopwatch.StartNew();
                for (int i = 0; i < repeats; i++)
                {
                    using CVCIEFile next = buffer.Read(path);
                    Assert.Same(first.Data, next.Data);
                }
                timer.Stop();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.True(allocated < expected.Data.LongLength, "Repeated reads must not allocate even one full RAW input array.");
                Assert.Equal(expectedHash, SHA256.HashData(first.Data));
                results.Add(new { Path = path, expected.Cols, expected.Rows, expected.Bpp, expected.Channels,
                    PayloadBytes = expected.Data.Length, Repeats = repeats, ManagedAllocatedBytes = allocated,
                    AverageReadMs = timer.Elapsed.TotalMilliseconds / repeats });
            }
        }
        string report = Environment.GetEnvironmentVariable("COLORVISION_CVRAW_REPORT_DIR") ?? root;
        Directory.CreateDirectory(report);
        File.WriteAllText(Path.Combine(report, "cvraw-reused-pixels.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        buffer.Clear();
    }

    private static FieldInfo Field<T>(string name) => typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static CVRawOpen Opener(ImageView view) => (CVRawOpen)view.IEditorToolFactory.IImageOpens[".cvraw"];
    private static SemaphoreSlim OpenGate(ImageView view) => (SemaphoreSlim)Field<CVRawOpen>("_rawOpenGate").GetValue(Opener(view))!;
    private static byte[]? Pixels(ImageView view)
        => (byte[]?)Field<CvRawPixelBuffer>("pixels").GetValue(Field<CVRawOpen>("_rawPixels").GetValue(Opener(view))!);
    private static ImageView CreateView() => WpfTestHost.Invoke(() =>
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
        view.IEditorToolFactory.IImageOpens[".cvraw"] = new CVRawOpen(view.EditorContext);
        return view;
    });
    private static async Task DisplayAsync(ImageView view, Action action)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object? sender, ImageViewImageSourceLoadedEventArgs args) => completion.TrySetResult();
        WpfTestHost.Invoke(() => { view.ImageSourceLoaded += OnLoaded; action(); });
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        finally { WpfTestHost.Invoke(() => view.ImageSourceLoaded -= OnLoaded); }
    }
    public void Dispose()
    {
        CVFileReadCache.Release();
        foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
        Directory.Delete(root);
    }
}
