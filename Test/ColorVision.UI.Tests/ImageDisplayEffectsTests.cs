using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Presentation.PseudoColor;
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

    private sealed class EffectsFixture : IDisposable
    {
        internal ImageSource? Source { get; set; }
        internal int SelectedChannel { get; set; }
        internal bool OwnerDisposed { get; set; }
        internal ImageProcessingContext Context { get; }
        internal PseudoColorState State => Context.DisplayEffects.PseudoColor;

        internal EffectsFixture()
        {
            Guid documentId = Guid.NewGuid();
            Context = new ImageProcessingContext(new ImageViewConfig(), new DrawCanvas(), Dispatcher.CurrentDispatcher,
                new ImageProcessingContextBinding
                {
                    IsInitialized = () => false,
                    GetDocumentInstanceId = () => documentId,
                    IsDisposed = () => OwnerDisposed,
                    GetImageRevision = () => 1,
                    AcquireImageFrame = () => null,
                    IsCurrentImageRevision = revision => !OwnerDisposed && revision == 1,
                    NotifySourcePixelsChanged = () => { },
                    CommitSourcePixels = value => Source = value,
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
