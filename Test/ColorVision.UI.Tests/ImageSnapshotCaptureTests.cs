using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Output;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageSnapshotCaptureTests
{
    [Fact]
    public async Task SnapshotStaWorkerReusesOneStaThreadAcrossConcurrentRequests()
    {
        ConcurrentBag<int> threadIds = [];
        ConcurrentBag<ApartmentState> apartmentStates = [];

        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            SnapshotStaWorker.RunAsync(() =>
            {
                threadIds.Add(Environment.CurrentManagedThreadId);
                apartmentStates.Add(Thread.CurrentThread.GetApartmentState());
            }, CancellationToken.None)));

        Assert.Single(threadIds.Distinct());
        Assert.All(apartmentStates, state => Assert.Equal(ApartmentState.STA, state));
    }

    [Fact]
    public async Task SnapshotEncodeWorkerUsesBoundedMtaConcurrency()
    {
        int active = 0;
        int maximumActive = 0;
        ConcurrentBag<ApartmentState> apartmentStates = [];

        await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            using SnapshotEncodeWorker.Reservation reservation =
                await SnapshotEncodeWorker.ReserveAsync(CancellationToken.None);
            await reservation.RunAsync(() =>
            {
                apartmentStates.Add(Thread.CurrentThread.GetApartmentState());
                int current = Interlocked.Increment(ref active);
                int observed;
                while ((observed = Volatile.Read(ref maximumActive)) < current
                    && Interlocked.CompareExchange(ref maximumActive, current, observed) != observed)
                {
                }
                Thread.Sleep(20);
                Interlocked.Decrement(ref active);
            }, CancellationToken.None);
        }));

        Assert.InRange(maximumActive, 1, SnapshotEncodeWorker.MaxConcurrency);
        Assert.All(apartmentStates, state => Assert.NotEqual(ApartmentState.STA, state));
    }

    [Fact]
    public async Task DetachedCapturesKeepTheirPixelsAcrossSourceMutationAndBufferRelease()
    {
        string firstPath = Path.Combine(Path.GetTempPath(), $"ColorVision-Snapshot-{Guid.NewGuid():N}.png");
        string secondPath = Path.Combine(Path.GetTempPath(), $"ColorVision-Snapshot-{Guid.NewGuid():N}.png");
        (ImageViewSnapshot first, ImageViewSnapshot second) = WpfTestHost.Invoke(() =>
        {
            // A standalone canvas is enough: capture has no ImageView, plugin or tool lifetime dependency.
            DrawCanvas canvas = new();
            WriteableBitmap source = new(1, 1, 96, 96, PixelFormats.Bgr24, null);
            ImageSnapshotCapture capture = new();
            source.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 255, 0, 0 }, 3, 0);
            ImageViewSnapshot first = capture.CaptureForBackgroundSave(
                canvas, source, 96, 96, includeOverlays: false,
                () => throw new InvalidOperationException("Source-only capture must not commit drawing edits."))!;
            Assert.NotNull(first);

            source.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 0, 0, 255 }, 3, 0);
            ImageViewSnapshot second = capture.CaptureForBackgroundSave(
                canvas, source, 96, 96, includeOverlays: false,
                () => throw new InvalidOperationException("Source-only capture must not commit drawing edits."))!;
            Assert.NotNull(second);

            source.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 0, 255, 0 }, 3, 0);
            capture.ReleaseBuffer();
            return (first, second);
        });

        try
        {
            await Task.WhenAll(
                ImageSnapshotEncoder.SaveSnapshotExportsAsync(first, new ImageViewSnapshotExportOptions { SourceFileName = firstPath }),
                ImageSnapshotEncoder.SaveSnapshotExportsAsync(second, new ImageViewSnapshotExportOptions { SourceFileName = secondPath }));

            Assert.Equal(new byte[] { 255, 0, 0 }, ReadBgrPixel(firstPath));
            Assert.Equal(new byte[] { 0, 0, 255 }, ReadBgrPixel(secondPath));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    private static byte[] ReadBgrPixel(string path)
    {
        using FileStream stream = File.OpenRead(path);
        BitmapSource frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        FormatConvertedBitmap bitmap = new(frame, PixelFormats.Bgr24, null, 0);
        byte[] pixels = new byte[3];
        bitmap.CopyPixels(pixels, 3, 0);
        return pixels;
    }
}
