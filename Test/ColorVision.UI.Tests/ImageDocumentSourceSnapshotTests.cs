using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Documents;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ImageDocumentSourceSnapshotTests
{
    [Fact]
    public void AlgorithmCaptureKeepsPrivatePixelsWithoutCreatingANativeFrame()
    {
        WpfTestHost.Invoke(() =>
        {
            byte[] original = [10, 20, 30, 40];
            WriteableBitmap source = CreateBitmap(original);
            using ImageView view = CreateView(source);
            ImageProcessingContext context = view.EditorContext.ProcessingContext;
            long revision = context.ImageRevision;
            Assert.Null(CurrentNativeFrame(view.Document));

            (AlgorithmImageBuffer image, long capturedRevision) = ImageAlgorithmInputFactory.AcquireCurrentFrame(context);
            using (image)
            {
                Assert.Equal(revision, capturedRevision);
                Assert.True(context.IsCurrentImageRevision(capturedRevision));
                Assert.Null(CurrentNativeFrame(view.Document));
                Assert.Equal(original, image.Data.ToArray());

                source.WritePixels(new Int32Rect(0, 0, 4, 1), new byte[] { 90, 80, 70, 60 }, 4, 0);

                Assert.Equal(original, image.Data.ToArray());
                Assert.Same(source, context.ViewBitmapSource);
                Assert.Null(CurrentNativeFrame(view.Document));
            }
        });
    }

    [Fact]
    public void CaptureDetectsACompatibilityReplacementEvenWhenNoNativeFrameWasCreated()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            WriteableBitmap first = CreateBitmap([1, 2, 3, 4]);
            WriteableBitmap replacement = CreateBitmap([5, 6, 7, 8]);
            document.AssignSource(first);
            (ImageSource? source, long revision) = document.CaptureSourceState();
            Assert.Same(first, source);
            int changes = 0;
            document.Changed += (_, _, _) => changes++;

            document.AssignSource(replacement);
            (ImageSource? currentSource, long currentRevision) = document.CaptureSourceState();
            (ImageSource? repeatedSource, long repeatedRevision) = document.CaptureSourceState();

            Assert.Same(replacement, currentSource);
            Assert.Equal(revision + 1, currentRevision);
            Assert.Same(replacement, repeatedSource);
            Assert.Equal(currentRevision, repeatedRevision);
            Assert.Equal(1, changes);
            Assert.False(document.IsCurrent(revision));
            Assert.True(document.IsCurrent(currentRevision));
            Assert.Null(CurrentNativeFrame(document));
        });
    }

    [Fact]
    public void MetadataCaptureRetiresTheNativeCacheWhileAnExistingLeaseRemainsReadable()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            byte[] original = [1, 2, 3, 4];
            byte[] replacementPixels = [5, 6, 7, 8];
            document.AssignSource(CreateBitmap(original));
            using ImageFrameLease previous = Assert.IsType<ImageFrameLease>(document.AcquireFrame());
            long revision = previous.Revision;

            WriteableBitmap replacement = CreateBitmap(replacementPixels);
            document.AssignSource(replacement);
            (ImageSource? source, long currentRevision) = document.CaptureSourceState();

            Assert.Same(replacement, source);
            Assert.Equal(revision + 1, currentRevision);
            Assert.False(document.IsCurrent(previous.Revision));
            Assert.Null(CurrentNativeFrame(document));
            Assert.Equal(original, ReadLease(previous));

            using ImageFrameLease current = Assert.IsType<ImageFrameLease>(document.AcquireFrame());
            Assert.Equal(currentRevision, current.Revision);
            Assert.Equal(replacementPixels, ReadLease(current));
            Assert.Equal(original, ReadLease(previous));
        });
    }

    [Fact]
    public void NativeAcquisitionRebindsTheSourceAfterAWorkerInvalidatesTheInitialCapture()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            byte[] original = [1, 2, 3, 4];
            WriteableBitmap source = CreateBitmap(original);
            document.AssignSource(source);
            long revision = document.Revision;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object frames = typeof(ImageDocument).GetField("_frames", flags)!.GetValue(document)!;
            object frameSync = typeof(ImageFrameStore).GetField("_sync", flags)!.GetValue(frames)!;
            object contentSync = typeof(ImageDocument).GetField("_contentSync", flags)!.GetValue(document)!;
            FieldInfo cachedSource = typeof(ImageDocument).GetField("_cachedSource", flags)!;
            using ManualResetEventSlim frameLocked = new();
            Task invalidation = Task.Run(() =>
            {
                lock (frameSync)
                {
                    frameLocked.Set();
                    // AcquireFrame first records the WPF source, then waits for this lock.
                    // This is a lifecycle-state gate; the deadline only detects a stalled test.
                    bool captured = SpinWait.SpinUntil(() =>
                    {
                        Thread.MemoryBarrier();
                        return ReferenceEquals(cachedSource.GetValue(document), source);
                    }, TimeSpan.FromSeconds(5));
                    Assert.True(captured, "Watchdog: AcquireFrame did not capture its source before requesting the frame-store lock.");
                    // The UI is now waiting for frameSync. Bound this check too so a future
                    // capture that incorrectly keeps contentSync cannot deadlock the WPF host.
                    bool contentAvailable = Monitor.TryEnter(contentSync, TimeSpan.FromSeconds(5));
                    if (contentAvailable) Monitor.Exit(contentSync);
                    Assert.True(contentAvailable, "Watchdog: source capture retained the content lock while waiting for the frame store.");
                    document.Invalidate(ImageDocumentMutationKind.SourcePixelsChanged);
                }
            });
            ImageFrameLease? lease = null;
            try
            {
                Assert.True(frameLocked.Wait(TimeSpan.FromSeconds(5)), "Watchdog: the worker did not take the frame-store lock.");
                lease = Assert.IsType<ImageFrameLease>(document.AcquireFrame());
                Assert.Equal(revision + 1, lease.Revision);
                Assert.Equal(document.Revision, lease.Revision);
                Assert.Equal(original, ReadLease(lease));

                WriteableBitmap replacement = CreateBitmap([5, 6, 7, 8]);
                document.AssignSource(replacement);
                (ImageSource? currentSource, long currentRevision) = document.CaptureSourceState();

                Assert.Same(replacement, currentSource);
                Assert.Equal(lease.Revision + 1, currentRevision);
                Assert.False(document.IsCurrent(lease.Revision));
                Assert.Null(CurrentNativeFrame(document));
                Assert.Equal(original, ReadLease(lease));
            }
            finally
            {
                lease?.Dispose();
                invalidation.GetAwaiter().GetResult();
            }
        });
    }

    [Fact]
    public void CaptureReconcilesASourceReplacedAfterAReentrantNativeAcquisition()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            WriteableBitmap first = CreateBitmap([1, 2, 3, 4]);
            byte[] intermediatePixels = [5, 6, 7, 8];
            WriteableBitmap intermediate = CreateBitmap(intermediatePixels);
            byte[] finalPixels = [9, 10, 11, 12];
            WriteableBitmap final = CreateBitmap(finalPixels);
            document.AssignSource(first);
            long revision = document.CaptureSourceState().Revision;
            ImageFrameLease? reentrantLease = null;
            List<(long Previous, long Current)> changes = [];
            bool capturedIntermediate = false;
            bool assignedFinal = false;
            document.Changed += (_, previous, current) =>
            {
                changes.Add((previous, current));
                if (capturedIntermediate) return;
                capturedIntermediate = true;
                Assert.Same(intermediate, document.CaptureSourceState().Source);
                reentrantLease = Assert.IsType<ImageFrameLease>(document.AcquireFrame());
            };
            document.Changed += (_, _, _) =>
            {
                if (assignedFinal) return;
                assignedFinal = true;
                document.AssignSource(final);
            };

            try
            {
                document.AssignSource(intermediate);
                (ImageSource? source, long currentRevision) = document.CaptureSourceState();

                Assert.Same(final, source);
                Assert.Equal(revision + 2, currentRevision);
                Assert.Equal(new[] { (revision, revision + 1), (revision + 1, revision + 2) }, changes);
                Assert.Null(CurrentNativeFrame(document));
                ImageFrameLease intermediateLease = Assert.IsType<ImageFrameLease>(reentrantLease);
                Assert.Equal(revision + 1, intermediateLease.Revision);
                Assert.False(document.IsCurrent(intermediateLease.Revision));
                Assert.Equal(intermediatePixels, ReadLease(intermediateLease));
                using ImageFrameLease finalLease = Assert.IsType<ImageFrameLease>(document.AcquireFrame());
                Assert.Equal(currentRevision, finalLease.Revision);
                Assert.Equal(finalPixels, ReadLease(finalLease));
            }
            finally
            {
                reentrantLease?.Dispose();
            }
        });
    }

    [Fact]
    public void StartingAPreviewRejectsASourceReplacedByTheCaptureRetirementCallback()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageView view = CreateView(CreateBitmap([1, 2, 3, 4]));
            ImageProcessingContext context = view.EditorContext.ProcessingContext;
            _ = view.Document.CaptureSourceState();
            WriteableBitmap intermediate = CreateBitmap([5, 6, 7, 8]);
            WriteableBitmap final = CreateBitmap([9, 10, 11, 12]);
            context.ViewBitmapSource = intermediate;
            bool replaced = false;
            view.Document.Changed += (_, _, _) =>
            {
                if (replaced) return;
                replaced = true;
                context.ViewBitmapSource = final;
            };

            Assert.Throws<OperationCanceledException>(() => ImageAlgorithmPreviewSession.Start(context));

            Assert.True(replaced);
            Assert.Same(final, context.ViewBitmapSource);
            Assert.False(context.HasActiveAlgorithmPreview);
            Assert.Null(context.Presentation.FunctionImage);
            Assert.Null(CurrentNativeFrame(view.Document));
        });
    }

    [Theory]
    [InlineData("pixels")]
    [InlineData("source-reference")]
    [InlineData("cancel")]
    [InlineData("dispose")]
    [InlineData("guard")]
    public async Task SegmentedAcquisitionRejectsAnOwnerInvalidatedBetweenBatches(string boundary)
    {
        await WpfTestHost.Invoke(async () =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            document.AssignSource(CreateBitmap([1, 2, 3, 4]));
            using ImageFrameLease previous = Assert.IsType<ImageFrameLease>(document.AcquireFrame());
            (WriteableBitmap source, byte[] pixels, int stride) = CreateSegmentedSource();
            document.AssignSource(source);
            long revision = document.CaptureSourceState().Revision;
            Assert.Null(CurrentNativeFrame(document));
            using CancellationTokenSource cancellation = new();
            bool mayContinue = true;
            WriteableBitmap replacement = CreateBitmap([101, 102, 103, 104]);
            using CompletedBatchInterleave interleave = new(document, () =>
            {
                Assert.Null(CurrentNativeFrame(document));
                switch (boundary)
                {
                    case "pixels":
                        pixels[0] ^= 0x80;
                        source.WritePixels(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight), pixels, stride, 0);
                        document.Invalidate(ImageDocumentMutationKind.SourcePixelsChanged);
                        break;
                    case "source-reference":
                        document.AssignSource(replacement);
                        Assert.Equal(revision, document.Revision);
                        break;
                    case "cancel":
                        cancellation.Cancel();
                        break;
                    case "dispose":
                        document.Dispose();
                        break;
                    case "guard":
                        mayContinue = false;
                        break;
                    default:
                        throw new InvalidOperationException(boundary);
                }
            });
            Task<ImageFrameLease?> acquisition = document.AcquireFrameAsync(source, revision, () => mayContinue, cancellation.Token);
            ImageFrameLease? acquired = null;
            try
            {
                try { acquired = await acquisition.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (OperationCanceledException) when (boundary == "cancel" && cancellation.IsCancellationRequested) { }
                await interleave.Completion.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.True(interleave.CompletedCopyBatch);
                Assert.Null(acquired);
                Assert.Null(CurrentNativeFrame(document));
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, ReadLease(previous));
                Assert.False(source.IsFrozen);
                if (boundary == "dispose")
                {
                    Assert.True(document.IsDisposed);
                    Assert.Null(document.Source);
                    Assert.False(document.IsCurrent(revision));
                }
                else
                {
                    Assert.Same(boundary == "source-reference" ? replacement : source, document.Source);
                    Assert.Equal(boundary is "pixels" or "source-reference" ? revision + 1 : revision, document.Revision);
                    Assert.Equal(boundary is "cancel" or "guard", document.IsCurrent(revision));
                }
            }
            finally
            {
                interleave.Dispose();
                cancellation.Cancel();
                acquired?.Dispose();
                try
                {
                    ImageFrameLease? remaining = await acquisition.WaitAsync(TimeSpan.FromSeconds(10));
                    if (!ReferenceEquals(remaining, acquired)) remaining?.Dispose();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            }
        });
    }

    [Theory]
    [InlineData("none")]
    [InlineData("concurrent-winner")]
    [InlineData("freeze")]
    public async Task SegmentedAcquisitionCopiesPackedPixelsOrReusesAConcurrentWinner(string boundary)
    {
        await WpfTestHost.Invoke(async () =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            document.AssignSource(CreateBitmap([1, 2, 3, 4]));
            using ImageFrameLease previous = Assert.IsType<ImageFrameLease>(document.AcquireFrame());
            (WriteableBitmap source, byte[] expected, int stride) = CreateSegmentedSource();
            document.AssignSource(source);
            long revision = document.CaptureSourceState().Revision;
            Assert.Null(CurrentNativeFrame(document));
            ImageFrameLease? winner = null;
            using CompletedBatchInterleave interleave = new(document, () =>
            {
                Assert.Null(CurrentNativeFrame(document));
                if (boundary == "concurrent-winner") winner = Assert.IsType<ImageFrameLease>(document.AcquireFrame());
                else if (boundary == "freeze") source.Freeze();
            });
            using CancellationTokenSource cancellation = new();
            Task<ImageFrameLease?> acquisition = document.AcquireFrameAsync(source, revision, () => true, cancellation.Token);
            ImageFrameLease? acquired = null;
            try
            {
                acquired = Assert.IsType<ImageFrameLease>(await acquisition.WaitAsync(TimeSpan.FromSeconds(10)));
                await interleave.Completion.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.True(interleave.CompletedCopyBatch);
                Assert.Equal(revision, acquired.Revision);
                Assert.Equal(stride, acquired.Image.stride);
                Assert.Equal(expected, ReadLease(acquired, expected.Length));
                Assert.Same(source, document.Source);
                Assert.Equal(boundary == "freeze", source.IsFrozen);
                Assert.Equal(revision, document.Revision);
                Assert.NotNull(CurrentNativeFrame(document));
                if (boundary == "concurrent-winner")
                    Assert.Equal(Assert.IsType<ImageFrameLease>(winner).Image.pData, acquired.Image.pData);
                else
                    Assert.Null(winner);

                document.Dispose();
                Assert.Null(CurrentNativeFrame(document));
                Assert.Equal(expected, ReadLease(acquired, expected.Length));
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, ReadLease(previous));
            }
            finally
            {
                interleave.Dispose();
                cancellation.Cancel();
                acquired?.Dispose();
                winner?.Dispose();
                try
                {
                    ImageFrameLease? remaining = await acquisition.WaitAsync(TimeSpan.FromSeconds(10));
                    if (!ReferenceEquals(remaining, acquired)) remaining?.Dispose();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            }
        });
    }

    private static (WriteableBitmap Source, byte[] Pixels, int Stride) CreateSegmentedSource()
    {
        const int width = 1025;
        const int height = 2730;
        const int stride = width * 3;
        byte[] pixels = new byte[stride * height];
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = (byte)((index * 17 + index / stride * 31 + 7) % 251);
        WriteableBitmap source = new(width, height, 96, 96, PixelFormats.Bgr24, null);
        source.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        Assert.True(source.BackBufferStride > stride);
        int batchBytes = (int)typeof(ImageDocument).GetField("FrameCopyBatchBytes", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        Assert.True(pixels.Length > batchBytes, "Fixture must cross a real source-copy batch boundary.");
        return (source, pixels, stride);
    }

    private sealed class CompletedBatchInterleave : IDisposable
    {
        private static readonly FieldInfo OperationMethod = typeof(DispatcherOperation).GetField("_method", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private readonly ImageDocument _document;
        private readonly Action _successor;
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private DispatcherOperation? _batch;
        private DispatcherOperation? _queuedSuccessor;

        internal CompletedBatchInterleave(ImageDocument document, Action successor)
        {
            _document = document;
            _successor = successor;
            _dispatcher.Hooks.OperationStarted += OperationStarted;
            _dispatcher.Hooks.OperationCompleted += OperationCompleted;
        }

        internal Task Completion => _completion.Task;
        internal bool CompletedCopyBatch { get; private set; }

        private void OperationStarted(object? sender, DispatcherHookEventArgs args)
        {
            if (_batch != null || args.Operation.Priority != DispatcherPriority.Background
                || OperationMethod.GetValue(args.Operation) is not Delegate callback
                || !callback.Method.Name.Contains("CopyNextBatch", StringComparison.Ordinal)
                || callback.Target == null) return;
            bool ownsDocument = callback.Target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => ReferenceEquals(field.GetValue(callback.Target), _document));
            if (ownsDocument) _batch = args.Operation;
        }

        private void OperationCompleted(object? sender, DispatcherHookEventArgs args)
        {
            if (!ReferenceEquals(_batch, args.Operation) || CompletedCopyBatch) return;
            CompletedCopyBatch = true;
            // This batch has finished copying and unlocked. Send runs before the next
            // Background batch, even if its continuation has already queued that batch.
            _queuedSuccessor = _dispatcher.InvokeAsync(() =>
            {
                try { _successor(); _completion.TrySetResult(); }
                catch (Exception error) { _completion.TrySetException(error); }
            }, DispatcherPriority.Send);
        }

        public void Dispose()
        {
            _dispatcher.Hooks.OperationStarted -= OperationStarted;
            _dispatcher.Hooks.OperationCompleted -= OperationCompleted;
            _queuedSuccessor?.Abort();
        }
    }

    private static ImageView CreateView(WriteableBitmap source)
    {
        ResourceDictionary resources = Application.Current.Resources;
        resources["TextBox.Small"] = new Style(typeof(TextBox));
        resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        resources["ToolBarImage"] = new Style(typeof(Image));
        resources["BaseStyle"] = new Style(typeof(Control));
        resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
        ImageView view = new();
        view.SetImageSource(source, enableEditorImageServices: false, configureDefaultLayerController: false);
        return view;
    }

    private static WriteableBitmap CreateBitmap(byte[] pixels)
    {
        WriteableBitmap bitmap = new(4, 1, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 4, 1), pixels, 4, 0);
        return bitmap;
    }

    private static byte[] ReadLease(ImageFrameLease lease, int length = 4)
    {
        byte[] pixels = new byte[length];
        Marshal.Copy(lease.Image.pData, pixels, 0, pixels.Length);
        return pixels;
    }

    private static object? CurrentNativeFrame(ImageDocument document)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object frames = typeof(ImageDocument).GetField("_frames", flags)!.GetValue(document)!;
        return typeof(ImageFrameStore).GetField("_current", flags)!.GetValue(frames);
    }
}
