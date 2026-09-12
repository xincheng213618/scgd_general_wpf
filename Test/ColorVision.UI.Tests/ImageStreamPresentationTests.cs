using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Presentation;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ImageStreamPresentationTests
{
    [Fact]
    public void SelectingCompositeRetiresThePreviousAlgorithmPreviewWithoutChangingPixels()
    {
        WpfTestHost.Invoke(() =>
        {
            using StreamFixture fixture = new((_, _) => throw new InvalidOperationException());
            ImageProcessingContext context = fixture.Context;
            long revision = context.ImageRevision;
            Assert.True(context.TryBeginAlgorithmPreviewSession(Guid.NewGuid(), context.DocumentInstanceId, revision,
                () => context.Presentation.RestoreSource(),
                () => context.Presentation.Publish(GrayPixel(219), null), out var claim));

            new ImageChannelPresenter(context).SelectChannel(-1);

            Assert.False(context.HasActiveAlgorithmPreview);
            Assert.False(context.TryPublishAlgorithmPreview(claim, () => throw new InvalidOperationException("An old algorithm cannot reclaim the display.")));
            Assert.Equal(revision, context.ImageRevision);
            Assert.Same(context.ViewBitmapSource, context.Presentation.DisplaySource);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceNotificationClosingStreamPreventsFramePublication(bool dispose)
    {
        WpfTestHost.Invoke(() =>
        {
            using StreamFixture fixture = new((_, _) => throw new InvalidOperationException());
            Guid sourceId = Guid.NewGuid();
            fixture.Context.DocumentScopeChanged += (_, _) =>
            {
                if (dispose) fixture.Stream.Dispose();
                else fixture.Stream.ResetSource(sourceId);
            };

            fixture.Stream.Submit(GrayPixel(44), sourceId);

            Assert.Empty(fixture.Presented);
            Assert.False(fixture.Stream.IsActive);
        });
    }

    [Fact]
    public void SourceNotificationSubmittingAnotherStreamPreservesNewOwner()
    {
        WpfTestHost.Invoke(() =>
        {
            using StreamFixture fixture = new((_, _) => throw new InvalidOperationException());
            Guid oldSourceId = Guid.NewGuid();
            Guid newSourceId = Guid.NewGuid();
            bool submittedReplacement = false;
            fixture.Context.DocumentScopeChanged += (_, _) =>
            {
                if (submittedReplacement) return;
                submittedReplacement = true;
                fixture.Stream.Submit(GrayPixel(219), newSourceId);
            };

            fixture.Stream.Submit(GrayPixel(44), oldSourceId);

            Assert.True(fixture.Stream.IsActive);
            Assert.Equal(new byte[] { 219 }, fixture.Presented.ToArray());
            Assert.Equal(219, ReadFirstByte((BitmapSource)fixture.Context.ViewBitmapSource));
            Assert.Same(fixture.Context.ViewBitmapSource, fixture.Context.Presentation.DisplaySource);

            fixture.Stream.ResetSource(oldSourceId);
            fixture.Stream.RefreshCurrent();
            Assert.True(fixture.Stream.IsActive);
            Assert.Equal(new byte[] { 219, 219 }, fixture.Presented.ToArray());
            fixture.Stream.ResetSource(newSourceId);
            Assert.False(fixture.Stream.IsActive);
        });
    }

    [Fact]
    public void SourceNotificationSelectingNewDisplayWinsOverPublishingFrame()
    {
        WpfTestHost.Invoke(() =>
        {
            using StreamFixture fixture = new((_, _) => throw new InvalidOperationException());
            WriteableBitmap newerChoice = GrayPixel(219);
            fixture.Context.DocumentScopeChanged += (_, _) => fixture.Context.Presentation.Publish(newerChoice, newerChoice);

            fixture.Stream.Submit(GrayPixel(44), Guid.NewGuid());

            Assert.Same(newerChoice, fixture.Context.Presentation.DisplaySource);
            Assert.Equal(44, ReadFirstByte((BitmapSource)fixture.Context.ViewBitmapSource));
            Assert.Empty(fixture.Presented);
            Assert.False(fixture.Stream.IsActive);
        });
    }

    [Fact]
    public void UnprocessedFrameOwnsFrozenPixelsInsteadOfProducerScratch()
    {
        WpfTestHost.Invoke(() =>
        {
            using StreamFixture fixture = new((_, _) => throw new InvalidOperationException("Raw display must not invoke a processor."));
            WriteableBitmap scratch = GrayPixel(12);
            Guid sourceId = Guid.NewGuid();
            fixture.Stream.Submit(scratch, sourceId);
            BitmapSource first = Assert.IsAssignableFrom<BitmapSource>(fixture.Context.ViewBitmapSource);
            Assert.NotSame(scratch, first);
            Assert.True(first.IsFrozen);

            scratch.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 201 }, 1, 0);
            Assert.Equal(12, ReadFirstByte(first));
            Assert.Equal(12, ReadFirstByte((BitmapSource)fixture.Context.Presentation.DisplaySource!));
            fixture.Stream.Submit(scratch, sourceId);

            Assert.Equal(201, ReadFirstByte((BitmapSource)fixture.Context.ViewBitmapSource));
            Assert.Equal(12, ReadFirstByte(first));
            Assert.Equal(new byte[] { 12, 201 }, fixture.Presented.ToArray());
        });
    }

    [Fact]
    public async Task BusyProcessorKeepsOnlyNewestPendingFrameAndIndependentSourcePixels()
    {
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        ConcurrentQueue<byte> processed = new();
        StreamFixture fixture = WpfTestHost.Invoke(() => new StreamFixture((source, _) =>
        {
            if (processed.IsEmpty)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("The test did not release the first processor.");
            }
            byte value = Marshal.ReadByte(source.pData);
            processed.Enqueue(value);
            return BgrPixel((byte)(value + 10));
        }, enablePseudoColor: true));
        Guid sourceId = Guid.NewGuid();
        try
        {
            WpfTestHost.Invoke(() => fixture.Stream.Submit(GrayPixel(1), sourceId));
            Assert.True(await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(10))));
            WpfTestHost.Invoke(() =>
            {
                WriteableBitmap scratch = GrayPixel(2);
                fixture.Stream.Submit(scratch, sourceId);
                scratch.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 3 }, 1, 0);
                fixture.Stream.Submit(scratch, sourceId);
                scratch.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 99 }, 1, 0);
            });
            release.Set();
            await WpfTestHost.Invoke(() => fixture.Stream.Completion).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(new byte[] { 1, 3 }, processed.ToArray());
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(new byte[] { 1, 3 }, fixture.Presented.ToArray());
                Assert.Equal(3, ReadFirstByte((BitmapSource)fixture.Context.ViewBitmapSource));
                Assert.Equal(13, ReadFirstByte((BitmapSource)fixture.Context.Presentation.DisplaySource!));
            });
        }
        finally
        {
            release.Set();
            await WpfTestHost.Invoke(() => fixture.Stream.Completion).WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(fixture.Dispose);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementOrDisposalRejectsAProcessingFrame(bool disposeStream)
    {
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        StreamFixture fixture = WpfTestHost.Invoke(() => new StreamFixture((source, _) =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("The test did not release the processor.");
            return BgrPixel(Marshal.ReadByte(source.pData));
        }, enablePseudoColor: true));
        try
        {
            WpfTestHost.Invoke(() => fixture.Stream.Submit(GrayPixel(42), Guid.NewGuid()));
            Assert.True(await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(10))));
            WpfTestHost.Invoke(() =>
            {
                if (disposeStream) fixture.Stream.Dispose();
                fixture.ReplaceSource(GrayPixel(177));
            });
            release.Set();
            await WpfTestHost.Invoke(() => fixture.Stream.Completion).WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                Assert.Empty(fixture.Presented);
                Assert.False(fixture.Stream.IsActive);
                Assert.Equal(177, ReadFirstByte((BitmapSource)fixture.Context.ViewBitmapSource));
                Assert.Equal(177, ReadFirstByte((BitmapSource)fixture.Context.Presentation.DisplaySource!));
            });
        }
        finally
        {
            release.Set();
            await WpfTestHost.Invoke(() => fixture.Stream.Completion).WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(fixture.Dispose);
        }
    }

    [Fact]
    public async Task ProcessorFailurePublishesThatFramesSourceWithoutLeavingOldDisplay()
    {
        StreamFixture fixture = WpfTestHost.Invoke(() => new StreamFixture(
            (_, _) => throw new InvalidOperationException("Expected processor failure."), enablePseudoColor: true));
        try
        {
            WpfTestHost.Invoke(() => fixture.Stream.Submit(GrayPixel(86), Guid.NewGuid()));
            await WpfTestHost.Invoke(() => fixture.Stream.Completion).WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(new byte[] { 86 }, fixture.Presented.ToArray());
                Assert.Null(fixture.Context.FunctionImage);
                Assert.Same(fixture.Context.ViewBitmapSource, fixture.Context.Presentation.DisplaySource);
                Assert.Equal(86, ReadFirstByte((BitmapSource)fixture.Context.ViewBitmapSource));
            });
        }
        finally { WpfTestHost.Invoke(fixture.Dispose); }
    }

    private sealed class StreamFixture : IDisposable
    {
        private ImageSource? _source = GrayPixel(0);
        private long _revision = 1;
        private bool _disposed;
        internal ImageProcessingContext Context { get; }
        internal ImageStreamPresentation Stream { get; }
        internal List<byte> Presented { get; } = [];

        internal StreamFixture(Func<HImage, PseudoColorFrameRequest, HImage> process, bool enablePseudoColor = false)
        {
            Guid documentId = Guid.NewGuid();
            Context = new ImageProcessingContext(new ImageViewConfig(), new DrawCanvas(), Dispatcher.CurrentDispatcher,
                new ImageProcessingContextBinding
                {
                    IsInitialized = () => false,
                    GetDocumentInstanceId = () => documentId,
                    IsDisposed = () => _disposed,
                    GetImageRevision = () => _revision,
                    AcquireImageFrame = () => null,
                    IsCurrentImageRevision = revision => !_disposed && revision == _revision,
                    NotifySourcePixelsChanged = AdvanceRevision,
                    CommitSourcePixels = value =>
                    {
                        _source = value;
                        AdvanceRevision();
                    },
                    GetViewBitmapSource = () => _source,
                    SetViewBitmapSource = value => _source = value,
                    GetSelectedLayerSourceChannelIndex = () => 0,
                    SetImageSource = ReplaceSource,
                    UpdateZoomAndScale = () => { },
                });
            Stream = new ImageStreamPresentation(Context, process);
            if (enablePseudoColor)
            {
                Context.DisplayEffects.PseudoColor.IsEnabled = true;
                // This fixture exercises the stream processor; invalidate the separately scheduled still preview.
                Context.DisplayEffects.Invalidate();
            }
            Stream.FramePresented += (_, source) => Presented.Add(ReadFirstByte(source));
        }

        private void AdvanceRevision()
        {
            _revision++;
            Context.Presentation.RestoreSourceForDocumentMutation();
            Context.NotifyDocumentScopeChanged();
        }

        internal void ReplaceSource(ImageSource source)
        {
            _source = source;
            AdvanceRevision();
            Context.Presentation.RestoreSource();
        }

        public void Dispose()
        {
            _disposed = true;
            Stream.Dispose();
            Context.StreamPresentation.Dispose();
            Context.DisplayEffects.Dispose();
            Context.DisposeAlgorithmOverlays();
        }
    }

    private static WriteableBitmap GrayPixel(byte value)
    {
        WriteableBitmap bitmap = new(1, 1, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new[] { value }, 1, 0);
        return bitmap;
    }

    private static HImage BgrPixel(byte value)
    {
        HImage image = new() { cols = 1, rows = 1, channels = 3, depth = 8, stride = 3, pData = Marshal.AllocCoTaskMem(3) };
        Marshal.Copy(new[] { value, value, value }, 0, image.pData, 3);
        return image;
    }

    private static byte ReadFirstByte(BitmapSource source)
    {
        byte[] pixel = new byte[Math.Max(1, source.Format.BitsPerPixel / 8)];
        source.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, pixel.Length, 0);
        return pixel[0];
    }
}
