using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Documents;
using ColorVision.ImageEditor.Presentation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ImageDocumentPresentationTests
{
    [Fact]
    public void ClearingImageCancelsFitAlreadyQueuedByWorker()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewResources();
            using ImageView view = new();
            view.SetImageSource(new WriteableBitmap(80, 60, 96, 96, PixelFormats.Gray8, null), false, false);
            StaTest.Run(view.UpdateZoomAndScale);
            view.Clear();
            view.SetImageSource(new WriteableBitmap(160, 120, 96, 96, PixelFormats.Gray8, null), false, false);
            Matrix requested = new(3, 0, 0, 3, 11, 13);
            view.Zoombox1.RestoreView(requested);
            Window window = new() { Content = view, Width = 400, Height = 300,
                Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.Equal(requested, view.Zoombox1.ContentMatrix);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void QueuedNavigationCannotRepopulateDisposedView()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewResources();
            using ImageView view = new();
            StaTest.Run(() => view.AppendImage("late-image.png", open: false));
            view.Dispose();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Assert.Empty(view.ImageGroupItems);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacingOrClearingImageCancelsItsPendingViewportFit(bool clear)
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewResources();
            using ImageView view = new();
            // Keep nonzero layout bounds after clear so an uncancelled fit would remain observable.
            view.ImageShow.Width = 160;
            view.ImageShow.Height = 120;
            view.SetImageSource(new WriteableBitmap(80, 60, 96, 96, PixelFormats.Gray8, null), false, false);
            view.Zoombox1.ZoomToContentRect(new Rect(10, 10, 30, 20));
            var next = new WriteableBitmap(160, 120, 96, 96, PixelFormats.Gray8, null);
            if (clear)
            {
                view.Clear();
            }
            else view.SetImageSource(next, false, false);

            Window window = new() { Content = view, Width = 400, Height = 300,
                Left = -30000, Top = -30000, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.Equal(Matrix.Identity, view.Zoombox1.ContentMatrix);
                if (clear) Assert.Null(view.ImageShow.Source);
                else Assert.Same(next, view.ImageShow.Source);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void DisplayPublicationWithoutDocumentSourceDoesNotCreateAPixelFrame()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            using DrawCanvas canvas = new();
            ImagePresentation presentation = new(document, canvas);
            WriteableBitmap display = GrayPixel(83);
            long revision = document.Revision;

            presentation.Publish(display, display);

            Assert.Same(display, presentation.DisplaySource);
            Assert.Same(display, presentation.FunctionImage);
            Assert.Null(document.Source);
            Assert.Null(document.AcquireFrame());
            Assert.Equal(revision, document.Revision);
        });
    }

    [Fact]
    public void ImageViewFrameEntryDoesNotUseDisplayPixelsWhenDocumentHasNoSource()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewResources();
            using ImageView view = new();
            WriteableBitmap display = GrayPixel(157);
            long revision = view.Document.Revision;

            view.Presentation.Publish(display, display);

            Assert.Same(display, view.ImageShow.Source);
            Assert.Null(view.Document.Source);
            Assert.Null(view.AcquireImageFrame());
            Assert.Null(view.EditorContext.ProcessingContext.AcquireImageFrame());
            Assert.Equal(revision, view.Document.Revision);
        });
    }

    [Fact]
    public void ProcessingContextCommitsThroughTheSessionOwnerAndRetiresOneRevision()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            using DrawCanvas canvas = new();
            WriteableBitmap original = GrayPixel(19);
            document.AssignSource(original);
            using ImageFrameLease originalLease = document.AcquireFrame()!;
            long originalRevision = document.Revision;
            List<(ImageSource? Source, long Previous, long Current)> notifications = [];
            using ImageEditorSession session = new(document, (previous, current) => notifications.Add((document.Source, previous, current)));
            ImagePresentation presentation = new(document, canvas);
            ImageProcessingContext context = new(new ImageViewConfig(), canvas, Dispatcher.CurrentDispatcher,
                new ImageProcessingContextBinding
                {
                    IsInitialized = () => false,
                    GetDocumentInstanceId = () => document.Id,
                    IsDisposed = () => document.IsDisposed,
                    GetImageRevision = () => document.Revision,
                    AcquireImageFrame = () => document.AcquireFrame(),
                    IsCurrentImageRevision = document.IsCurrent,
                    // A commit must use the owner's command, not reconstruct it from legacy setters.
                    NotifySourcePixelsChanged = () => throw new InvalidOperationException("Use the session commit command."),
                    CommitSourcePixels = session.CommitSourcePixels,
                    GetViewBitmapSource = () => document.Source,
                    SetViewBitmapSource = _ => throw new InvalidOperationException("Use the session commit command."),
                    GetSelectedLayerSourceChannelIndex = () => 0,
                    SetImageSource = _ => throw new InvalidOperationException("Pixel commit must not reopen the source."),
                    UpdateZoomAndScale = () => { },
                },
                ColorVision.ImageEditor.Algorithms.ImageAlgorithmPlatform.Runtime,
                presentation);
            session.Attach(context);

            WriteableBitmap first = GrayPixel(73);
            context.CommitSourcePixels(first);
            Assert.Same(first, document.Source);
            Assert.Equal(originalRevision + 1, document.Revision);
            Assert.Equal(((ImageSource?)first, originalRevision, originalRevision + 1), Assert.Single(notifications));
            Assert.Equal(19, Marshal.ReadByte(originalLease.Image.pData));

            WriteableBitmap second = GrayPixel(127);
            session.CommitSourcePixels(second);
            Assert.Same(second, context.ViewBitmapSource);
            Assert.Equal(originalRevision + 2, document.Revision);
            Assert.Equal(2, notifications.Count);
            Assert.Equal(((ImageSource?)second, originalRevision + 1, originalRevision + 2), notifications[1]);
        });
    }

    [Fact]
    public void DisposedDocumentRejectsLegacyAssignmentsWithoutRetainingNewPixels()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            ImagePresentation presentation = new(document, new DrawCanvas());
            document.Dispose();

            Assert.Throws<ObjectDisposedException>(() => document.AssignSource(GrayPixel(25)));
            Assert.Throws<ObjectDisposedException>(() => presentation.FunctionImage = GrayPixel(42));
            presentation.Publish(GrayPixel(57), null);

            Assert.Null(document.Source);
            Assert.Null(presentation.FunctionImage);
            Assert.Null(presentation.DisplaySource);
        });
    }

    [Fact]
    public void LaterDisplayChoiceRejectsEarlierResultWithoutChangingSourcePixels()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            WriteableBitmap source = GrayPixel(37);
            document.AssignSource(source);
            DrawCanvas canvas = new();
            ImagePresentation presentation = new(document, canvas);
            presentation.RestoreSource();
            using ImageFrameLease lease = document.AcquireFrame()!;
            long sourceRevision = document.Revision;

            ImagePresentationRequest earlier = presentation.BeginRequest();
            ImagePresentationRequest latest = presentation.BeginRequest();
            WriteableBitmap selectedDisplay = GrayPixel(211);
            Assert.True(presentation.TryPublish(latest, selectedDisplay, selectedDisplay));
            Assert.False(presentation.TryPublish(earlier, GrayPixel(99), GrayPixel(99)));

            Assert.Same(selectedDisplay, presentation.DisplaySource);
            Assert.Same(source, document.Source);
            Assert.Equal(sourceRevision, document.Revision);
            Assert.Equal(37, Marshal.ReadByte(lease.Image.pData));

            presentation.RestoreSource();
            Assert.Same(source, presentation.DisplaySource);
            Assert.Null(presentation.FunctionImage);
            Assert.False(presentation.TryPublish(latest, selectedDisplay, selectedDisplay));
        });
    }

    [Fact]
    public void SourceReplacementRetiresRevisionWhileExistingLeaseSurvivesDocumentDisposal()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            document.AssignSource(GrayPixel(19));
            using ImageFrameLease oldLease = document.AcquireFrame()!;
            ImagePresentation presentation = new(document, new DrawCanvas());
            ImagePresentationRequest oldRequest = presentation.BeginRequest();

            document.AssignSource(GrayPixel(173));
            using ImageFrameLease replacement = document.AcquireFrame()!;
            Assert.True(replacement.Revision > oldLease.Revision);
            Assert.False(document.IsCurrent(oldLease.Revision));
            Assert.False(presentation.TryPublish(oldRequest, GrayPixel(255), null));
            Assert.Equal(19, Marshal.ReadByte(oldLease.Image.pData));
            Assert.Equal(173, Marshal.ReadByte(replacement.Image.pData));

            document.Dispose();
            Assert.Equal(19, Marshal.ReadByte(oldLease.Image.pData));
            Assert.Equal(173, Marshal.ReadByte(replacement.Image.pData));
            Assert.False(document.IsCurrent(replacement.Revision));
        });
    }

    [Fact]
    public void EqualRevisionAndGenerationDoNotAllowPublicationAcrossDocuments()
    {
        WpfTestHost.Invoke(() =>
        {
            using ImageDocument firstDocument = new(Dispatcher.CurrentDispatcher);
            using ImageDocument secondDocument = new(Dispatcher.CurrentDispatcher);
            WriteableBitmap firstSource = GrayPixel(23);
            WriteableBitmap secondSource = GrayPixel(157);
            firstDocument.AssignSource(firstSource);
            secondDocument.AssignSource(secondSource);
            ImagePresentation first = new(firstDocument, new DrawCanvas());
            ImagePresentation second = new(secondDocument, new DrawCanvas());
            ImagePresentationRequest firstRequest = first.BeginRequest();
            ImagePresentationRequest secondRequest = second.BeginRequest();
            Assert.Equal(firstRequest.SourceRevision, secondRequest.SourceRevision);
            Assert.Equal(firstRequest.Generation, secondRequest.Generation);
            Assert.NotEqual(firstRequest.DocumentId, secondRequest.DocumentId);

            Assert.False(second.TryPublish(firstRequest, firstSource, null));
            Assert.True(second.TryPublish(secondRequest, secondSource, null));
            firstDocument.Dispose();
            Assert.True(second.IsCurrent(secondRequest));
            Assert.Same(secondSource, second.DisplaySource);
        });
    }

    [Fact]
    public void VectorDocumentKeepsLogicalCanvasWithoutInventingAPixelFrame()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawingImage source = new(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 23.5, 17.25))));
            using ImageDocument document = new(Dispatcher.CurrentDispatcher);
            document.AssignSource(source);
            ImagePresentation presentation = new(document, new DrawCanvas());
            presentation.RestoreSource();

            Assert.Same(source, document.Source);
            Assert.Same(source, presentation.DisplaySource);
            Assert.Equal(23.5, source.Width);
            Assert.Equal(17.25, source.Height);
            Assert.Null(document.AcquireFrame());
        });
    }

    private static WriteableBitmap GrayPixel(byte value)
    {
        WriteableBitmap bitmap = new(1, 1, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new[] { value }, 1, 0);
        return bitmap;
    }

    private static void EnsureImageViewResources()
    {
        Application application = Application.Current!;
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
