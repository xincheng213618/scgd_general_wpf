using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Presentation;
using ColorVision.ImageEditor.Presentation.PseudoColor;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ImageDisplayEffectsTests
{
    [Fact]
    public void PseudoColorRequestRequiresEnabledEffectSourceAndLiveOwner()
    {
        WpfTestHost.Invoke(() =>
        {
            using EffectsFixture fixture = new();
            Assert.False(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out _));
            fixture.State.IsEnabled = true;
            fixture.Context.DisplayEffects.Invalidate();
            Assert.False(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out _));

            fixture.Source = new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray8, null);
            Assert.True(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out _));
            fixture.State.IsEnabled = false;
            fixture.Context.DisplayEffects.Invalidate();
            Assert.False(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out _));

            fixture.State.IsEnabled = true;
            fixture.Context.DisplayEffects.Invalidate();
            fixture.OwnerDisposed = true;
            Assert.False(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out _));
            fixture.OwnerDisposed = false;
            fixture.Context.DisplayEffects.Dispose();
            Assert.False(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out _));
        });
    }

    [Fact]
    public async Task PseudoColorRequestCapturesIndependentNormalizedParametersWithoutPublishing()
    {
        EffectsFixture fixture = WpfTestHost.Invoke(() =>
        {
            EffectsFixture result = new();
            result.Source = new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray8, null);
            result.Context.Presentation.Publish(result.Source, null);
            result.SelectedChannel = 2;
            result.State.SliderMinimum = 10;
            result.State.SliderMaximum = 100;
            result.State.SliderValueStart = 200;
            result.State.SliderValueEnd = -20;
            result.State.ColormapTypes = ColormapTypes.COLORMAP_TURBO;
            result.State.IsAutoSetRange = true;
            result.State.DataMin = 12;
            result.State.DataMax = 97;
            result.State.IsEnabled = true;
            result.Context.DisplayEffects.Invalidate();
            return result;
        });
        try
        {
            PseudoColorFrameRequest snapshot = await Task.Run(() =>
            {
                Assert.True(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out PseudoColorFrameRequest request));
                return request;
            });
            Assert.Equal(new PseudoColorFrameRequest(10, 100, ColormapTypes.COLORMAP_TURBO, 2, true, 12, 97), snapshot);
            Assert.True(snapshot.HasValidAutoRange);

            WpfTestHost.Invoke(() =>
            {
                fixture.State.SliderValueStart = 40;
                fixture.State.SliderValueEnd = 60;
                fixture.State.DataMax = 12;
                fixture.SelectedChannel = 0;
                fixture.Context.DisplayEffects.Invalidate();
                Assert.True(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out PseudoColorFrameRequest current));
                Assert.Equal(40u, current.Min);
                Assert.Equal(60u, current.Max);
                Assert.Equal(0, current.Channel);
                Assert.False(current.HasValidAutoRange);
                Assert.True(snapshot.HasValidAutoRange);
                Assert.Equal(2, snapshot.Channel);
                Assert.Equal(10u, snapshot.Min);
                Assert.Equal(1, fixture.Context.ImageRevision);
                Assert.Same(fixture.Source, fixture.Context.Presentation.DisplaySource);
                Assert.Null(fixture.Context.FunctionImage);
            });
        }
        finally { WpfTestHost.Invoke(fixture.Dispose); }
    }

    [Fact]
    public void AutoRangeDefersFrameAcquisitionUntilPseudoColorIsEnabled()
    {
        WpfTestHost.Invoke(() =>
        {
            using EffectsFixture fixture = new() { Initialized = true };

            fixture.State.IsAutoSetRange = true;
            fixture.Context.DisplayEffects.ConfigureForImage();

            Assert.Equal(0, fixture.AcquireCount);

            fixture.State.IsEnabled = true;

            Assert.Equal(1, fixture.AcquireCount);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContinuousPseudoColorPublishesIndependentWorkerSnapshotsAndRejectsResetWork(bool reset)
    {
        using ManualResetEventSlim processing = new();
        using ManualResetEventSlim finishProcessing = new();
        EffectsFixture fixture = WpfTestHost.Invoke(() => new EffectsFixture());
        ImageStreamPresentation stream = WpfTestHost.Invoke(() => new ImageStreamPresentation(fixture.Context, (source, _) =>
        {
            Assert.False(fixture.Context.Dispatcher.CheckAccess());
            processing.Set();
            Assert.True(finishProcessing.Wait(TimeSpan.FromSeconds(10)));
            byte[] pixels = new byte[4];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    pixels[y * 2 + x] = (byte)(Marshal.ReadByte(source.pData, y * source.stride + x) + 10);
            IntPtr output = Marshal.AllocCoTaskMem(pixels.Length);
            Marshal.Copy(pixels, 0, output, pixels.Length);
            return new HImage { pData = output, rows = 2, cols = 2, channels = 1, depth = 8, stride = 2 };
        }));
        try
        {
            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap source = new(2, 2, 96, 96, PixelFormats.Gray8, null);
                source.WritePixels(new Int32Rect(0, 0, 2, 2), new byte[] { 1, 2, 3, 4 }, 2, 0);
                fixture.Source = source;
                fixture.State.IsEnabled = true;
                fixture.Context.DisplayEffects.Invalidate();
                fixture.Context.Presentation.Publish(source, null);
                stream.Submit(source, Guid.NewGuid());
                source.WritePixels(new Int32Rect(0, 0, 2, 2), new byte[4], 2, 0);
            });
            Assert.True(processing.Wait(TimeSpan.FromSeconds(10)));
            if (reset) WpfTestHost.Invoke(stream.Reset);
            finishProcessing.Set();
            await stream.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                if (reset)
                {
                    Assert.False(stream.IsActive);
                    Assert.Null(fixture.Context.FunctionImage);
                    Assert.Equal(1, fixture.Context.ImageRevision);
                    return;
                }
                var source = Assert.IsType<WriteableBitmap>(fixture.Source);
                var display = Assert.IsType<WriteableBitmap>(fixture.Context.FunctionImage);
                Assert.True(source.IsFrozen);
                Assert.True(display.IsFrozen);
                Assert.NotSame(source, display);
                byte[] pixels = new byte[4];
                source.CopyPixels(pixels, 2, 0);
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, pixels);
                display.CopyPixels(pixels, 2, 0);
                Assert.Equal(new byte[] { 11, 12, 13, 14 }, pixels);
                Assert.Equal(2, fixture.Context.ImageRevision);
                Assert.Same(display, fixture.Context.Presentation.DisplaySource);
            });
        }
        finally
        {
            finishProcessing.Set();
            WpfTestHost.Invoke(() => { stream.Dispose(); fixture.Dispose(); });
        }
    }

    [Fact]
    public async Task ContinuousPlainChannelRejectsInFlightPseudoColorAndPublishesOnlyLatestFrame()
    {
        using ManualResetEventSlim processing = new();
        using ManualResetEventSlim finishProcessing = new();
        EffectsFixture fixture = WpfTestHost.Invoke(() => new EffectsFixture { SelectedChannel = 2 });
        int pseudoCalls = 0;
        List<(Guid SourceId, WriteableBitmap Source)> presented = new();
        Guid sourceId = Guid.NewGuid();
        byte[] latestPixels = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };
        PseudoColorFrameRequest pseudoSettings = default;
        ImageStreamPresentation stream = WpfTestHost.Invoke(() =>
        {
            ImageStreamPresentation result = new(fixture.Context, (_, _) =>
            {
                Assert.False(fixture.Context.Dispatcher.CheckAccess());
                Interlocked.Increment(ref pseudoCalls);
                processing.Set();
                Assert.True(finishProcessing.Wait(TimeSpan.FromSeconds(10)));
                byte[] pixels = { 201, 202, 203, 204 };
                IntPtr output = Marshal.AllocCoTaskMem(pixels.Length);
                Marshal.Copy(pixels, 0, output, pixels.Length);
                return new HImage { pData = output, rows = 2, cols = 2, channels = 1, depth = 8, stride = 2 };
            });
            result.FramePresented += (id, source) => presented.Add((id, source));
            return result;
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap source = new(2, 2, 96, 96, PixelFormats.Bgr24, null);
                source.WritePixels(new Int32Rect(0, 0, 2, 2), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 6, 0);
                fixture.Source = source;
                fixture.Context.Presentation.Publish(source, null);
                fixture.State.IsEnabled = true;
                fixture.Context.DisplayEffects.Invalidate();
                Assert.True(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out pseudoSettings));
                stream.Submit(source, sourceId);
            });
            Assert.True(processing.Wait(TimeSpan.FromSeconds(10)));
            WpfTestHost.Invoke(() =>
            {
                fixture.State.IsEnabled = false;
                fixture.Context.DisplayEffects.Invalidate();
                Assert.False(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out PseudoColorFrameRequest current));
                // Keep every captured setting equal so only the mode can retire the old work.
                Assert.Equal(pseudoSettings, current);
                var source = Assert.IsType<WriteableBitmap>(fixture.Source);
                source.WritePixels(new Int32Rect(0, 0, 2, 2), new byte[] { 11, 12, 13, 21, 22, 23, 31, 32, 33, 41, 42, 43 }, 6, 0);
                stream.Submit(source, sourceId);
                source.WritePixels(new Int32Rect(0, 0, 2, 2), latestPixels, 6, 0);
                stream.Submit(source, sourceId);
                Assert.Empty(presented);
                Assert.Equal(1, fixture.Context.ImageRevision);
            });
            finishProcessing.Set();
            await stream.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(1, pseudoCalls);
                Assert.False(fixture.State.IsEnabled);
                Assert.Equal(2, fixture.SelectedChannel);
                Assert.Equal(2, fixture.Context.ImageRevision);
                var source = Assert.IsType<WriteableBitmap>(fixture.Source);
                var display = Assert.IsType<WriteableBitmap>(fixture.Context.FunctionImage);
                var publication = Assert.Single(presented);
                Assert.Equal(sourceId, publication.SourceId);
                Assert.Same(source, publication.Source);
                Assert.True(source.IsFrozen);
                Assert.True(display.IsFrozen);
                Assert.Equal(PixelFormats.Bgr24, source.Format);
                Assert.Equal(PixelFormats.Gray8, display.Format);
                Assert.NotSame(source, display);
                byte[] sourcePixels = new byte[12];
                source.CopyPixels(sourcePixels, 6, 0);
                Assert.Equal(latestPixels, sourcePixels);
                byte[] displayPixels = new byte[4];
                display.CopyPixels(displayPixels, 2, 0);
                Assert.Equal(new byte[] { 30, 60, 90, 120 }, displayPixels);
                Assert.Same(display, fixture.Context.Presentation.DisplaySource);
            });
        }
        finally
        {
            finishProcessing.Set();
            WpfTestHost.Invoke(() => { stream.Dispose(); fixture.Dispose(); });
        }
    }

    [Theory]
    [InlineData("direct-display")]
    [InlineData("pending-request")]
    [InlineData("no-final-frame")]
    [InlineData("foreign-source")]
    public void FrameSourceRestoreDeferralPreservesNewSelectionsAndFallsBackOnlyForItsOwnSource(string boundary)
    {
        WpfTestHost.Invoke(() =>
        {
            using EffectsFixture fixture = new();
            ImagePresentation presentation = fixture.Context.Presentation;
            WriteableBitmap oldGray = new(2, 2, 96, 96, PixelFormats.Gray8, null);
            WriteableBitmap newRgb = new(2, 2, 96, 96, PixelFormats.Bgr24, null);
            WriteableBitmap newerDisplay = new(2, 2, 96, 96, PixelFormats.Gray8, null);
            WriteableBitmap foreignSource = new(2, 2, 96, 96, PixelFormats.Bgr24, null);
            fixture.Source = oldGray;
            presentation.Publish(oldGray, oldGray);
            ImagePresentationRequest oldRequest = presentation.BeginRequest();
            ImagePresentationRequest? pendingRequest = null;
            ImageSource expectedDisplay = oldGray;
            ImageSource? expectedFunction = oldGray;

            using (presentation.DeferSourceRestoreForFrame(newRgb, fixture.Context.ImageRevision + 1))
            {
                fixture.Context.CommitSourcePixels(newRgb);
                switch (boundary)
                {
                    case "direct-display":
                        presentation.Publish(newerDisplay, newerDisplay);
                        expectedDisplay = newerDisplay;
                        expectedFunction = newerDisplay;
                        break;
                    case "pending-request":
                        pendingRequest = presentation.BeginRequest();
                        break;
                    case "no-final-frame":
                        expectedFunction = null;
                        break;
                    case "foreign-source":
                        fixture.Source = foreignSource;
                        expectedDisplay = foreignSource;
                        expectedFunction = null;
                        break;
                }
                Assert.Equal(2, fixture.Context.ImageRevision);
                Assert.False(presentation.IsCurrent(oldRequest));
                presentation.RestoreSourceForDocumentMutation();
                Assert.Same(expectedDisplay, presentation.DisplaySource);
                Assert.Same(expectedFunction, presentation.FunctionImage);
                if (pendingRequest is ImagePresentationRequest pending)
                    Assert.True(presentation.IsCurrent(pending));
            }

            if (boundary == "no-final-frame") expectedDisplay = newRgb;
            Assert.Same(expectedDisplay, presentation.DisplaySource);
            Assert.Same(expectedFunction, presentation.FunctionImage);
            Assert.Same(boundary == "foreign-source" ? foreignSource : newRgb, fixture.Source);
            Assert.False(presentation.TryPublish(oldRequest, oldGray, oldGray));
            if (pendingRequest is ImagePresentationRequest current)
            {
                Assert.True(presentation.IsCurrent(current));
                Assert.True(presentation.TryPublish(current, newerDisplay, newerDisplay));
                Assert.Same(newerDisplay, presentation.DisplaySource);
                Assert.Same(newerDisplay, presentation.FunctionImage);
            }
        });
    }

    [Fact]
    public async Task DisablingPseudoColorCannotOverwriteADisplaySelectedAfterChannelCapture()
    {
        TimeSpan watchdog = TimeSpan.FromSeconds(10);
        using ImageFrameStore store = new();
        store.Invalidate();
        TaskCompletionSource<ImageFrameLease> capturedFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? replacementPublication = null;
        int releaseCount = 0;
        byte[] sourcePixels = [11, 21, 31, 41, 51, 61, 71, 81, 91, 101, 111, 121];
        WriteableBitmap? source = null;
        WriteableBitmap? replacement = null;
        Guid documentId = Guid.Empty;
        long revision = 0;
        EffectsFixture fixture = WpfTestHost.Invoke(() =>
        {
            EffectsFixture result = new() { SelectedChannel = 2 };
            source = new WriteableBitmap(2, 2, 96, 96, PixelFormats.Bgr24, null);
            source.WritePixels(new Int32Rect(0, 0, 2, 2), sourcePixels, 6, 0);
            source.Freeze();
            replacement = new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray8, null);
            replacement.WritePixels(new Int32Rect(0, 0, 2, 2), new byte[] { 201, 202, 203, 204 }, 2, 0);
            replacement.Freeze();
            result.Source = source;
            result.Context.Presentation.Publish(source, null);
            documentId = result.Context.DocumentInstanceId;
            revision = result.Context.ImageRevision;
            result.State.IsAutoSetRange = false;
            result.AcquireFrame = () =>
            {
                ImageFrameLease lease = Assert.IsType<ImageFrameLease>(store.AcquireOrCreate(() =>
                {
                    IntPtr pixels = Marshal.AllocCoTaskMem(sourcePixels.Length);
                    Marshal.Copy(sourcePixels, 0, pixels, sourcePixels.Length);
                    return new HImage { pData = pixels, rows = 2, cols = 2, channels = 3, depth = 8, stride = 6 };
                }, image =>
                {
                    image.Dispose();
                    Interlocked.Increment(ref releaseCount);
                    released.TrySetResult(true);
                }));
                // Queue a real newer selection without pumping inside capture. Send precedes the prepared frame's UI commit.
                replacementPublication = result.Context.Dispatcher.InvokeAsync(
                    () => result.Context.Presentation.Publish(replacement, replacement), DispatcherPriority.Send).Task;
                capturedFrame.TrySetResult(lease);
                return lease;
            };
            return result;
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                fixture.State.IsEnabled = true;
                fixture.Context.DisplayEffects.Invalidate();
                fixture.State.IsEnabled = false;
            });
            ImageFrameLease lease = await capturedFrame.Task.WaitAsync(watchdog);
            Task publication = WpfTestHost.Invoke(() => Assert.IsAssignableFrom<Task>(replacementPublication));
            await publication.WaitAsync(watchdog);
            // Retire the store's reference: its release callback now deterministically waits for the controller's lease return.
            store.Dispose();
            await released.Task.WaitAsync(watchdog);
            Assert.Throws<ObjectDisposedException>(() => { _ = lease.Image; });
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(1, fixture.AcquireCount);
                Assert.Equal(1, Volatile.Read(ref releaseCount));
                Assert.False(fixture.State.IsEnabled);
                Assert.Same(replacement, fixture.Context.Presentation.DisplaySource);
                Assert.Same(replacement, fixture.Context.FunctionImage);
                Assert.Same(source, fixture.Source);
                Assert.Equal(documentId, fixture.Context.DocumentInstanceId);
                Assert.Equal(revision, fixture.Context.ImageRevision);
                byte[] actualSource = new byte[12];
                source!.CopyPixels(actualSource, 6, 0);
                Assert.Equal(sourcePixels, actualSource);
            });
        }
        finally { WpfTestHost.Invoke(fixture.Dispose); }
    }

    private sealed class EffectsFixture : IDisposable
    {
        internal ImageSource? Source { get; set; }
        internal int SelectedChannel { get; set; }
        internal bool OwnerDisposed { get; set; }
        internal bool Initialized { get; set; }
        internal int AcquireCount { get; private set; }
        internal Func<ImageFrameLease?>? AcquireFrame { get; set; }
        private long _revision = 1;
        internal ImageProcessingContext Context { get; }
        internal PseudoColorState State => Context.DisplayEffects.PseudoColor;

        internal EffectsFixture()
        {
            Guid documentId = Guid.NewGuid();
            Context = new ImageProcessingContext(new ImageViewConfig(), new DrawCanvas(), Dispatcher.CurrentDispatcher,
                new ImageProcessingContextBinding
                {
                    IsInitialized = () => Initialized,
                    GetDocumentInstanceId = () => documentId,
                    IsDisposed = () => OwnerDisposed,
                    GetImageRevision = () => _revision,
                    AcquireImageFrame = () =>
                    {
                        AcquireCount++;
                        return AcquireFrame?.Invoke();
                    },
                    IsCurrentImageRevision = revision => !OwnerDisposed && revision == _revision,
                    NotifySourcePixelsChanged = () => { },
                    CommitSourcePixels = value => { Source = value; _revision++; },
                    GetViewBitmapSource = () => Source,
                    SetViewBitmapSource = value => Source = value,
                    GetSelectedLayerSourceChannelIndex = () => SelectedChannel,
                    SetImageSource = value => Source = value,
                    UpdateZoomAndScale = () => { },
                });
        }

        public void Dispose()
        {
            OwnerDisposed = true;
            Context.StreamPresentation.Dispose();
            Context.DisplayEffects.Dispose();
            Context.DisposeAlgorithmOverlays();
        }
    }
}
