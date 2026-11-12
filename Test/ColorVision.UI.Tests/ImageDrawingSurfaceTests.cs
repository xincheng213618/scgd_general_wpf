using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageDrawingSurfaceTests
{
    [Theory]
    [InlineData(false, Stretch.Uniform)]
    [InlineData(false, Stretch.Fill)]
    [InlineData(true, Stretch.Uniform)]
    [InlineData(true, Stretch.Fill)]
    public void SeparateImageSurfacePreservesImageLayoutAndPixels(bool vector, Stretch stretch)
    {
        WpfTestHost.Invoke(() =>
        {
            DrawingImage drawing = new(new GeometryDrawing(Brushes.CornflowerBlue, null, new RectangleGeometry(new Rect(0, 0, 40, 20))));
            RenderTargetBitmap bitmap = new(40, 20, 96, 96, PixelFormats.Pbgra32);
            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen()) dc.DrawImage(drawing, new Rect(0, 0, 40, 20));
            bitmap.Render(visual);
            ImageSource source = vector ? drawing : bitmap;
            Image baseline = new() { Source = source, Stretch = stretch };
            using DrawCanvas surface = new() { Source = source, Stretch = stretch };

            Assert.Equal(Render(baseline), Render(surface));
            Assert.Equal(baseline.DesiredSize, surface.DesiredSize);
            Assert.Equal(baseline.RenderSize, surface.RenderSize);
            Assert.Empty(surface.Visuals);
            Assert.Same(surface, surface.GetVisual<Visual>(new Point(10, 10)));
            Assert.Empty(surface.GetVisuals(new RectangleGeometry(new Rect(-1, -1, 102, 102))));
        });
    }

    [Fact]
    public void ImageLayerDoesNotEnterAnnotationSelectionOrUndoHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawingImage image = new(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 100, 100))));
            using DrawCanvas surface = new() { Source = image };
            DrawingVisual annotation = new();
            using (DrawingContext dc = annotation.RenderOpen()) dc.DrawRectangle(Brushes.Red, null, new Rect(20, 20, 10, 10));
            surface.AddVisualCommand(annotation);
            byte[] pixels = Render(surface);

            Assert.Same(annotation, surface.GetVisual<DrawingVisual>(new Point(25, 25)));
            Assert.Equal(new[] { annotation }, surface.GetVisuals(new RectangleGeometry(new Rect(-1, -1, 102, 102))));
            Assert.Equal(255, pixels[(25 * 100 + 25) * 4 + 2]);
            Assert.Equal(0, pixels[(25 * 100 + 25) * 4 + 1]);
            surface.Undo();
            Assert.Empty(surface.Visuals);
            surface.Redo();
            Assert.Same(annotation, Assert.Single(surface.Visuals));
            surface.Clear();
            Assert.Empty(surface.Visuals);
            Assert.Same(image, surface.Source);
            Assert.Equal(255, Render(surface)[(25 * 100 + 25) * 4 + 1]);
        });
    }

    [Theory]
    [InlineData(192, 48, 1.125, 3.0, 1.25, 5.75, 2, 1, 3, 4, false)]
    [InlineData(96, 96, 2.75, 3.75, 0.5, 0.5, 2, 3, 2, 2, false)]
    [InlineData(192, 48, -0.75, 6.0, 5.75, 13.0, 0, 3, 8, 5, false)]
    [InlineData(96, 96, 6.25, 1.0, 2.25, 1.0, 3, 2, 2, 2, true)]
    public void RectangleCropReplacesTheImageWithThePixelsCoveredByTheAnnotation(double dpiX, double dpiY,
        double left, double top, double width, double height, int pixelX, int pixelY, int pixelWidth, int pixelHeight, bool stretch)
    {
        WpfTestHost.Invoke(() =>
        {
            byte[] original = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
            BitmapSource source = BitmapSource.Create(8, 8, dpiX, dpiY, PixelFormats.Gray8, null, original, 8);
            using DrawCanvas canvas = new() { Source = source };
            ImageProcessingContext context = new(new ImageViewConfig(), canvas, canvas.Dispatcher,
                new ImageProcessingContextBinding
                {
                    IsInitialized = () => true,
                    GetDocumentInstanceId = () => Guid.Empty,
                    IsDisposed = () => false,
                    GetImageRevision = () => 0,
                    AcquireImageFrame = () => null,
                    IsCurrentImageRevision = _ => true,
                    NotifySourcePixelsChanged = () => { },
                    CommitSourcePixels = pixels => canvas.Source = pixels,
                    GetViewBitmapSource = () => canvas.Source,
                    SetViewBitmapSource = pixels => canvas.Source = pixels,
                    GetSelectedLayerSourceChannelIndex = () => -1,
                    SetImageSource = pixels => canvas.Source = pixels,
                    UpdateZoomAndScale = () => { },
                });
            if (stretch)
            {
                canvas.Stretch = Stretch.Fill;
                canvas.Measure(new Size(16, 4));
                canvas.Arrange(new Rect(0, 0, 16, 4));
            }
            DVRectangle rectangle = new(new RectangleProperties { Rect = new Rect(left, top, width, height) });
            canvas.AddVisualCommand(rectangle);
            var menu = new DVRectangleDVContextMenu(canvas, context);
            MenuItem replace = menu.GetContextMenuItems(rectangle).Last();

            replace.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            BitmapSource cropped = Assert.IsAssignableFrom<BitmapSource>(canvas.Source);
            Assert.Equal(pixelWidth, cropped.PixelWidth);
            Assert.Equal(pixelHeight, cropped.PixelHeight);
            Assert.Equal(PixelFormats.Gray8, cropped.Format);
            byte[] pixels = new byte[pixelWidth * pixelHeight];
            cropped.CopyPixels(pixels, pixelWidth, 0);
            byte[] expected = [.. Enumerable.Range(pixelY, pixelHeight)
                .SelectMany(row => original.AsSpan(row * 8 + pixelX, pixelWidth).ToArray())];
            Assert.Equal(expected, pixels);
            Assert.Empty(canvas.Visuals);
        });
    }

    private static byte[] Render(FrameworkElement element)
    {
        element.Measure(new Size(100, 100));
        element.Arrange(new Rect(0, 0, 100, 100));
        element.UpdateLayout();
        RenderTargetBitmap target = new(100, 100, 96, 96, PixelFormats.Pbgra32);
        target.Render(element);
        byte[] pixels = new byte[100 * 100 * 4];
        target.CopyPixels(pixels, 100 * 4, 0);
        return pixels;
    }
}
