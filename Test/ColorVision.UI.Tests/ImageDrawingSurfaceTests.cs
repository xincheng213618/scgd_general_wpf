using ColorVision.ImageEditor;
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
