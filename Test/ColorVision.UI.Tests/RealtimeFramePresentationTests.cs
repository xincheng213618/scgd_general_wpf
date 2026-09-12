using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Realtime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class RealtimeFramePresentationTests
{
    [Theory]
    [InlineData(RealtimeFramePresenter.TransformNone, new byte[] { 1, 2, 3, 4 })]
    [InlineData(RealtimeFramePresenter.TransformFlipX, new byte[] { 3, 4, 1, 2 })]
    [InlineData(RealtimeFramePresenter.TransformFlipY, new byte[] { 2, 1, 4, 3 })]
    [InlineData(RealtimeFramePresenter.TransformFlipXY, new byte[] { 4, 3, 2, 1 })]
    public void SubmittedFramesPublishTheConfiguredTransformAsBothSourceAndDisplay(int transform, byte[] expected)
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewResources();
            using ImageView view = new();
            view.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
            view.Realtime.Configure(new RealtimeFrameOptions { AutoZoomOnFirstFrame = false, UpdateImageMetadata = false });
            byte[] input = [1, 2, 3, 4];

            Assert.True(view.Realtime.SubmitFrame(input, 2, 2, PixelFormats.Gray8, 2, input.Length, transform));
            DrainRenderQueue();

            BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(view.ViewBitmapSource);
            byte[] sourcePixels = new byte[4];
            source.CopyPixels(sourcePixels, 2, 0);
            Assert.Equal(expected, sourcePixels);
            Assert.Same(source, view.ImageShow.Source);
            Assert.True(source.IsFrozen);
            Assert.Null(view.FunctionImage);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, input);
        });
    }

    [Fact]
    public void ResetClearsItsPublishedCloneWithoutClearingAReplacementDocument()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewResources();
            using ImageView view = new();
            view.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
            view.Realtime.Configure(new RealtimeFrameOptions { AutoZoomOnFirstFrame = false });
            view.Realtime.SubmitFrame(new byte[] { 1, 2, 3, 4 }, 2, 2, PixelFormats.Gray8, 2);
            DrainRenderQueue();
            Assert.NotNull(view.ImageShow.Source);

            view.Realtime.Reset(clearImageSource: true);
            Assert.Null(view.ImageShow.Source);

            view.Realtime.SubmitFrame(new byte[] { 5, 6, 7, 8 }, 2, 2, PixelFormats.Gray8, 2);
            DrainRenderQueue();
            WriteableBitmap replacement = new(1, 1, 96, 96, PixelFormats.Gray8, null);
            replacement.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 91 }, 1, 0);
            view.SetImageSource(replacement, enableEditorImageServices: false, configureDefaultLayerController: false);
            view.Realtime.Reset(clearImageSource: true);
            DrainRenderQueue();

            Assert.Same(replacement, view.ViewBitmapSource);
            Assert.Same(replacement, view.ImageShow.Source);
        });
    }

    [Fact]
    public void NewFramesPreserveUserZoomUntilImageGeometryChanges()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewResources();
            using ImageView view = new();
            view.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
            view.Realtime.Configure(new RealtimeFrameOptions { AutoZoomOnFirstFrame = true });
            view.Measure(new Size(800, 600));
            view.Arrange(new Rect(0, 0, 800, 600));
            view.Realtime.SubmitFrame(new byte[] { 1, 2, 3, 4 }, 2, 2, PixelFormats.Gray8, 2);
            DrainRenderQueue(view.UpdateLayout);
            DrainRenderQueue(view.UpdateLayout);
            Matrix userTransform = new(4, 0, 0, 4, 13, 17);
            view.EditorContext.Zoombox.ContentMatrix = userTransform;

            view.Realtime.SubmitFrame(new byte[] { 5, 6, 7, 8 }, 2, 2, PixelFormats.Gray8, 2);
            DrainRenderQueue(view.UpdateLayout);
            DrainRenderQueue(view.UpdateLayout);
            Assert.Equal(userTransform, view.EditorContext.Zoombox.ContentMatrix);

            view.Realtime.SubmitFrame(new byte[] { 1, 2, 3, 4, 5, 6 }, 3, 2, PixelFormats.Gray8, 3);
            DrainRenderQueue(view.UpdateLayout);
            DrainRenderQueue(view.UpdateLayout);
            Assert.NotEqual(userTransform, view.EditorContext.Zoombox.ContentMatrix);
        });
    }

    [Fact]
    public void ThrowingFrameObserverDoesNotPreventTheNextFrameFromRendering()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewResources();
            using ImageView view = new();
            view.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
            view.Realtime.Configure(new RealtimeFrameOptions { AutoZoomOnFirstFrame = false });
            int calls = 0;
            view.EditorContext.ProcessingContext.StreamPresentation.FramePresented += (_, _) =>
            {
                if (++calls == 1) throw new InvalidOperationException("synthetic frame observer failure");
            };
            view.Realtime.SubmitFrame(new byte[] { 1, 2, 3, 4 }, 2, 2, PixelFormats.Gray8, 2);
            DrainRenderQueue();

            view.Realtime.SubmitFrame(new byte[] { 5, 6, 7, 8 }, 2, 2, PixelFormats.Gray8, 2);
            DrainRenderQueue();

            byte[] actual = new byte[4];
            Assert.IsAssignableFrom<BitmapSource>(view.ImageShow.Source).CopyPixels(actual, 2, 0);
            Assert.Equal(new byte[] { 5, 6, 7, 8 }, actual);
            Assert.Equal(2, calls);
        });
    }

    private static void DrainRenderQueue(Action? updateLayout = null)
    {
        DispatcherFrame frame = new();
        // The producer posts at Render priority. This lower-priority barrier observes its
        // completed publication without depending on a timer or a frame-rate assumption.
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            updateLayout?.Invoke();
            frame.Continue = false;
        }));
        Dispatcher.PushFrame(frame);
    }

    private static void EnsureImageViewResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
