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

    private static byte[] ReadLease(ImageFrameLease lease)
    {
        byte[] pixels = new byte[4];
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
