using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
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

public sealed class CVFileReadCacheTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"cvraw-cache-{Guid.NewGuid():N}");
    public CVFileReadCacheTests() { Directory.CreateDirectory(root); CVFileReadCache.IsEnabled = true; CVFileReadCache.Release(); }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 3)]
    [InlineData(16, 1)]
    [InlineData(16, 3)]
    public void DisabledCacheUsesFilesIncludingMetadataAndKeepsPixelBufferReuse(int bpp, int channels)
    {
        string path = Path.Combine(root, "toggle.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(17, 13, bpp, channels);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        CvRawPixelBuffer pixels = new();
        using CVCIEFile first = pixels.Read(path);
        byte[] reused = first.Data;

        CVFileReadCache.IsEnabled = false;
        var disabled = CVFileReadCache.GetSnapshot();
        Assert.False(disabled.IsEnabled);
        Assert.Equal(0, disabled.CapacityBytes);
        raw.Data[0] ^= 255;
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        CVFileMetadata.SetProperty(path, "test-json", 1, [1, 2, 3]);
        Assert.Equal(new byte[] { 1, 2, 3 }, CVFileMetadata.Read(path)["test-json"].Value);
        using (Stream file = CVFileReadCache.OpenRead(path)) Assert.IsType<FileStream>(file);
        AssertChannels(path, raw);
        using CVCIEFile disk = pixels.Read(path);
        Assert.Same(reused, disk.Data);
        Assert.Equal(raw.Data, disk.Data);
        var afterReads = CVFileReadCache.GetSnapshot();
        Assert.Null(afterReads.FilePath);
        Assert.Equal(0, afterReads.CapacityBytes);
        Assert.Equal(disabled.AllocationCount, afterReads.AllocationCount);
        Assert.Equal(disabled.HitCount, afterReads.HitCount);

        CVFileReadCache.IsEnabled = true;
        using CVCIEFile cached = pixels.Read(path);
        Assert.Same(reused, cached.Data);
        Assert.Equal(raw.Data, cached.Data);
        Assert.Equal(path, CVFileReadCache.GetSnapshot().FilePath);
        AssertCacheEqualsDisk(path);
    }

    [Fact]
    public void DisablingRetainsBorrowedPixelsUntilLastReaderReturnsAndBlocksNewCacheHits()
    {
        string path = Path.Combine(root, "borrowed.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(17, 13, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        using Stream first = CVFileReadCache.OpenRead(path);
        using Stream second = CVFileReadCache.OpenRead(path);
        CVFileReadCache.IsEnabled = false;
        Assert.True(CVFileReadCache.GetSnapshot().CapacityBytes > 0);
        Assert.Null(CVFileReadCache.GetSnapshot().FilePath);
        using (Stream fallback = CVFileReadCache.OpenRead(path)) Assert.IsType<FileStream>(fallback);
        Assert.Equal(HashFile(path), SHA256.HashData(first));
        first.Dispose();
        Assert.True(CVFileReadCache.GetSnapshot().CapacityBytes > 0);
        Assert.Equal(HashFile(path), SHA256.HashData(second));
        second.Dispose();
        Assert.Equal(0, CVFileReadCache.GetSnapshot().CapacityBytes);
    }

    [Fact]
    public void ReenablingDuringBorrowDoesNotPublishOldKeyOrReleaseAnActivePointer()
    {
        string path = Path.Combine(root, "reenable.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(17, 13, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        using Stream borrowed = CVFileReadCache.OpenRead(path);
        IntPtr pointer = SlotPointer();
        CVFileReadCache.IsEnabled = false;
        CVFileReadCache.IsEnabled = true;
        using (Stream fallback = CVFileReadCache.OpenRead(path)) Assert.IsType<FileStream>(fallback);
        Assert.Null(CVFileReadCache.GetSnapshot().FilePath);
        Assert.Equal(HashFile(path), SHA256.HashData(borrowed));
        borrowed.Dispose();
        AssertCacheEqualsDisk(path);
        Assert.Equal(pointer, SlotPointer());
    }

    [Fact]
    public void SavedFilesReuseOneSlotAcrossPathsChannelsAndBitDepths()
    {
        long allocations = CVFileReadCache.GetSnapshot().AllocationCount;
        IntPtr firstPointer = IntPtr.Zero;
        foreach (var shape in new[] { (257, 131, 16, 3), (17, 11, 8, 3), (17, 11, 16, 1), (19, 7, 16, 3) })
        {
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(shape.Item1, shape.Item2, shape.Item3, shape.Item4);
            string path = Path.Combine(root, $"{shape}.cvraw");
            Assert.True(CVFileUtil.WriteCVRaw(path, raw));
            CVFileReadCacheSnapshot snapshot = CVFileReadCache.GetSnapshot();
            Assert.Equal(path, snapshot.FilePath);
            Assert.Equal(allocations + 1, snapshot.AllocationCount);
            if (firstPointer == IntPtr.Zero) firstPointer = SlotPointer();
            Assert.Equal(firstPointer, SlotPointer());
            AssertCacheEqualsDisk(path);
            AssertChannels(path, raw);

            byte original = raw.Data[0];
            raw.Data[0] ^= 255; // Producer and subsequent consumers never own the cached pixels.
            using LocalFlowFrame loaded = LocalFrameFileService.Load(path);
            using var lease = loaded.Acquire();
            Assert.Equal(original, System.Runtime.InteropServices.Marshal.ReadByte(lease.RawPointer));
            System.Runtime.InteropServices.Marshal.WriteByte(lease.RawPointer, (byte)(original ^ 255));
            AssertCacheEqualsDisk(path);
        }
    }

    [Fact]
    public void MetadataUpdatesPatchTheTailAndOnlyGrowCapacityWhenNeeded()
    {
        string path = Path.Combine(root, "metadata.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(17, 13, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        IntPtr pointer = SlotPointer();
        long allocations = CVFileReadCache.GetSnapshot().AllocationCount;
        CVFileMetadata.SetProperty(path, "future", 9, [4, 5, 6]);
        for (byte i = 0; i < 5; i++) CVFileMetadata.SetProperty(path, "calibration", 1, [i, 8, 9]);
        Assert.Equal(pointer, SlotPointer());
        Assert.Equal(allocations, CVFileReadCache.GetSnapshot().AllocationCount);
        Assert.Equal(new byte[] { 4, 8, 9 }, CVFileMetadata.Read(path)["calibration"].Value);
        AssertCacheEqualsDisk(path);

        CVFileMetadata.SetProperty(path, "calibration", 1, new byte[128 * 1024]);
        Assert.Equal(allocations + 1, CVFileReadCache.GetSnapshot().AllocationCount);
        AssertCacheEqualsDisk(path);
        pointer = SlotPointer();
        CVFileMetadata.SetProperty(path, "calibration", 2, [1]);
        Assert.Equal(pointer, SlotPointer());
        Assert.Equal(new byte[] { 4, 5, 6 }, CVFileMetadata.Read(path)["future"].Value);
        AssertCacheEqualsDisk(path);
        AssertChannels(path, raw);
    }

    [Fact]
    public void MissesValidateFileChangesAndBusySlotFallsBackWithoutAnotherAllocation()
    {
        string first = Path.Combine(root, "first.cvraw"), second = Path.Combine(root, "second.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(17, 13, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(first, raw));
        File.Copy(first, second);
        long allocations = CVFileReadCache.GetSnapshot().AllocationCount;
        using (Stream borrowed = CVFileReadCache.OpenRead(first))
        {
            using Stream fallback = CVFileReadCache.OpenRead(second);
            Assert.IsType<FileStream>(fallback);
            Assert.Equal(first, CVFileReadCache.GetSnapshot().FilePath);
            Assert.Equal(allocations, CVFileReadCache.GetSnapshot().AllocationCount);
            Assert.Equal(HashFile(first), SHA256.HashData(borrowed));
        }
        byte[] bytes = File.ReadAllBytes(first);
        bytes[^1] ^= 255;
        File.WriteAllBytes(first, bytes);
        File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddMinutes(1));
        AssertCacheEqualsDisk(first);
        Assert.Equal(allocations, CVFileReadCache.GetSnapshot().AllocationCount);
        CVFileReadCache.Release();
        Assert.True(CVFileUtil.ReadCIEFileHeader(second, out _) > 0);
        Assert.Equal(0, CVFileReadCache.GetSnapshot().CapacityBytes); // Header probes do not warm a full image.
        AssertCacheEqualsDisk(second);
        File.Delete(second);
        Assert.Throws<FileNotFoundException>(() => CVFileReadCache.OpenRead(second));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverwriteAndReleaseWaitUntilBorrowedPixelsAreNoLongerInUse(bool release)
    {
        string path = Path.Combine(root, "active.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(19, 13, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        byte[] expected = HashFile(path);
        using Stream borrowed = CVFileReadCache.OpenRead(path);
        using ManualResetEventSlim started = new(), completed = new();
        Exception? failure = null;
        Thread worker = new(() =>
        {
            started.Set();
            try
            {
                if (release) CVFileReadCache.Release();
                else { raw.Data[0] ^= 255; Assert.True(CVFileUtil.WriteCVRaw(path, raw)); }
            }
            catch (Exception ex) { failure = ex; }
            finally { completed.Set(); }
        }) { IsBackground = true };
        worker.Start();
        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(SpinWait.SpinUntil(() => completed.IsSet || (worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(10)));
            Assert.False(completed.IsSet);
            Assert.Equal(expected, SHA256.HashData(borrowed));
        }
        finally { borrowed.Dispose(); Assert.True(completed.Wait(TimeSpan.FromSeconds(10))); }
        Assert.Null(failure);
        if (release) Assert.Equal(0, CVFileReadCache.GetSnapshot().CapacityBytes);
        else AssertCacheEqualsDisk(path);
    }

    [Fact]
    public void FailedSaveDoesNotPublishPartialPixels()
    {
        string path = Path.Combine(root, "failed.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(17, 13, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        Assert.False(CVFileUtil.WriteCVRaw(path, raw, raw.Data.Length, stream => stream.WriteByte(1)));
        Assert.Null(CVFileReadCache.GetSnapshot().FilePath);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        AssertCacheEqualsDisk(path);
    }

    [Fact]
    public void SeekingPayloadWriterKeepsDiskSemanticsWithoutPublishingUnwrittenCacheBytes()
    {
        string path = Path.Combine(root, "sparse.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(17, 13, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        Assert.True(CVFileUtil.WriteCVRaw(path, raw, raw.Data.Length, stream =>
        {
            stream.Seek(raw.Data.Length - 1, SeekOrigin.Current);
            stream.WriteByte(9);
        }));
        Assert.Null(CVFileReadCache.GetSnapshot().FilePath);
        AssertCacheEqualsDisk(path);
        Assert.True(CVFileUtil.ReadCVRaw(path, out CVCIEFile loaded));
        using (loaded)
        {
            Assert.All(loaded.Data[..^1], pixel => Assert.Equal(0, pixel));
            Assert.Equal(9, loaded.Data[^1]);
        }
    }

    [Fact]
    public async Task UnifiedReleaseClearsImageSlotAndKeepsFilesReadable()
    {
        string path = Path.Combine(root, "release.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(17, 13, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        long capacity = CVFileReadCache.GetSnapshot().CapacityBytes;
        var summary = await LocalCalibrationCacheService.ReleaseAllAsync(Array.Empty<DeviceCamera>());
        Assert.True(summary.Succeeded, string.Join("; ", summary.Errors));
        Assert.Equal(capacity, summary.ImageFileBytesReleased);
        Assert.Equal(0, LocalCalibrationCacheService.GetSnapshot().ImageFile.CapacityBytes);
        AssertCacheEqualsDisk(path);
    }

    [Fact]
    public async Task CvRawOpenReusesDisplayMemoryAndCachePixelsStayIndependent()
    {
        ImageView view = WpfTestHost.Invoke(() =>
        {
            Application app = Application.Current;
            app.Resources["TextBox.Small"] = new Style(typeof(TextBox));
            app.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            app.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            app.Resources["ToolBarImage"] = new Style(typeof(Image));
            app.Resources["BaseStyle"] = new Style(typeof(Control));
            app.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            app.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            ImageView created = new() { EnableEditorImageServices = false };
            created.IEditorToolFactory.IEditorTools.Clear();
            created.IEditorToolFactory.IImageOpens[".cvraw"] = new CVRawOpen(created.EditorContext);
            return created;
        });
        try
        {
            WriteableBitmap? previous = null;
            foreach (byte value in new byte[] { 10, 200, 60 })
            {
                string path = Path.Combine(root, $"display-{value}.cvraw");
                using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(7, 5, 8, 1);
                Array.Fill(raw.Data, value);
                Assert.True(CVFileUtil.WriteCVRaw(path, raw));
                long hits = CVFileReadCache.GetSnapshot().HitCount;
                TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnLoaded(object? sender, ImageViewImageSourceLoadedEventArgs args) => completion.TrySetResult();
                WpfTestHost.Invoke(() => { view.ImageSourceLoaded += OnLoaded; view.OpenImage(path); });
                try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
                finally { WpfTestHost.Invoke(() => view.ImageSourceLoaded -= OnLoaded); }
                WpfTestHost.Invoke(() =>
                {
                    Assert.IsType<CVRawOpen>(view.EditorContext.IImageOpen);
                    var current = Assert.IsType<WriteableBitmap>(view.ViewBitmapSource);
                    if (previous != null) Assert.Same(previous, current);
                    previous = current;
                    byte[] displayed = new byte[35];
                    current.CopyPixels(displayed, 7, 0);
                    Assert.All(displayed, pixel => Assert.Equal(value, pixel));
                    current.WritePixels(new Int32Rect(0, 0, 7, 5), new byte[35], 7, 0);
                });
                Assert.True(CVFileReadCache.GetSnapshot().HitCount > hits);
                AssertCacheEqualsDisk(path);
            }
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [CvRawSamplesFact]
    public void RealSamplesKeepEveryChannelAndReuseTheLargestSlot()
    {
        string sampleRoot = Environment.GetEnvironmentVariable("COLORVISION_CVRAW_SAMPLE_DIR")!;
        var files = Directory.EnumerateFiles(sampleRoot, "*.cvraw").Select(path =>
        {
            Assert.True(CVFileUtil.ReadCIEFileHeader(path, out CVCIEFile header) > 0);
            return (Path: path, Header: header, Length: new FileInfo(path).Length);
        }).GroupBy(file => (file.Header.Channels, file.Header.Bpp, Large: file.Length > 200_000_000))
          .Select(group => group.First()).OrderByDescending(file => file.Length).ToArray();
        Assert.True(files.Length >= 3, "Choose samples covering mono, RGB and different bit depths.");
        List<object> results = [];
        long initialAllocations = CVFileReadCache.GetSnapshot().AllocationCount;
        IntPtr pointer = IntPtr.Zero;
        foreach (var file in files)
        {
            byte[] originalHash = HashFile(file.Path);
            Assert.True(CVFileUtil.Read(file.Path, out CVCIEFile raw));
            using (raw)
            {
                string output = Path.Combine(root, Path.GetFileName(file.Path));
                Stopwatch timer = Stopwatch.StartNew();
                Assert.True(CVFileUtil.WriteCVRaw(output, raw));
                double writeMs = timer.Elapsed.TotalMilliseconds;
                foreach (var property in CVFileMetadata.Read(file.Path))
                    CVFileMetadata.SetProperty(output, property.Key, property.Value.Version, property.Value.Value);
                if (pointer == IntPtr.Zero) pointer = SlotPointer();
                Assert.Equal(pointer, SlotPointer());
                Assert.Equal(initialAllocations + 1, CVFileReadCache.GetSnapshot().AllocationCount);
                AssertCacheEqualsDisk(output);
                timer.Restart();
                AssertChannels(output, raw);
                double channelChecksMs = timer.Elapsed.TotalMilliseconds;
                // Time the same bounded copy loop; disk reads may already be served by the OS cache.
                byte[] copyBuffer = new byte[1024 * 1024];
                double TimeCopy(Stream source)
                {
                    using (source)
                    {
                        timer.Restart();
                        while (source.Read(copyBuffer, 0, copyBuffer.Length) != 0) { }
                        return timer.Elapsed.TotalMilliseconds;
                    }
                }
                double diskCopyMs = TimeCopy(File.OpenRead(output));
                double cacheCopyMs = TimeCopy(CVFileReadCache.OpenRead(output));
                // The same serializer with an unrelated suffix bypasses only the CVRAW cache.
                string diskOnly = output + ".disk-only";
                List<double> savedWithCache = [], savedToDisk = [];
                for (int pass = 0; pass < 3; pass++)
                {
                    timer.Restart();
                    Assert.True(CVFileUtil.WriteCVRaw(diskOnly, raw));
                    savedToDisk.Add(timer.Elapsed.TotalMilliseconds);
                    timer.Restart();
                    Assert.True(CVFileUtil.WriteCVRaw(output, raw));
                    savedWithCache.Add(timer.Elapsed.TotalMilliseconds);
                }
                Assert.Equal(HashFile(diskOnly), HashFile(output));
                AssertCacheEqualsDisk(output);
                results.Add(new { file.Path, raw.Cols, raw.Rows, raw.Bpp, raw.Channels, Bytes = raw.Data.Length,
                    WriteWithCacheMs = writeMs, ChannelChecksMs = channelChecksMs, DiskCopyMs = diskCopyMs,
                    CacheCopyMs = cacheCopyMs, DiskOnlyWriteMedianMs = savedToDisk.Order().ElementAt(1),
                    CachedWriteMedianMs = savedWithCache.Order().ElementAt(1),
                    Allocations = CVFileReadCache.GetSnapshot().AllocationCount - initialAllocations });
                Assert.Equal(originalHash, HashFile(file.Path));
            }
        }
        string reportDirectory = Environment.GetEnvironmentVariable("COLORVISION_CVRAW_REPORT_DIR") ?? root;
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(Path.Combine(reportDirectory, "cvraw-cache-samples.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AssertChannels(string path, CVCIEFile raw)
    {
        int channelBytes = raw.Cols * raw.Rows * (raw.Bpp / 8);
        for (int channel = 0; channel < raw.Channels; channel++)
        {
            Assert.True(CVFileUtil.ReadCIEFileChannel(path, channel, out CVCIEFile loaded));
            using (loaded) Assert.True(raw.Data.AsSpan(channel * channelBytes, channelBytes).SequenceEqual(loaded.Data));
        }
    }
    private static IntPtr SlotPointer() => (IntPtr)typeof(CVFileReadCache).GetField("buffer", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    private static byte[] HashFile(string path) { using Stream file = File.OpenRead(path); return SHA256.HashData(file); }
    private static void AssertCacheEqualsDisk(string path)
    {
        using Stream cached = CVFileReadCache.OpenRead(path);
        Assert.Equal(HashFile(path), SHA256.HashData(cached));
    }
    public void Dispose()
    {
        CVFileReadCache.Release();
        CVFileReadCache.IsEnabled = true;
        // Only this test's newly created, flat directory is touched.
        foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
        Directory.Delete(root);
    }
}

public sealed class CvRawSamplesFactAttribute : FactAttribute
{
    public CvRawSamplesFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COLORVISION_CVRAW_SAMPLE_DIR")))
            Skip = "Set COLORVISION_CVRAW_SAMPLE_DIR to run read-only source checks against local CVRAW samples.";
    }
}
