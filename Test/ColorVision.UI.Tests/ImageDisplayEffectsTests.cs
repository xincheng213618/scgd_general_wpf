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

    private sealed class EffectsFixture : IDisposable
    {
        internal ImageSource? Source { get; set; }
        internal int SelectedChannel { get; set; }
        internal bool OwnerDisposed { get; set; }
        internal bool Initialized { get; set; }
        internal int AcquireCount { get; private set; }
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
                        return null;
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
