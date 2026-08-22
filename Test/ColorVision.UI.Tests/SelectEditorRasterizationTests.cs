using ColorVision.ImageEditor.Draw;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class SelectEditorRasterizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoiRendererMatchesLegacyFullCanvasPixels(bool useFractionalBounds)
    {
        WpfTestHost.Invoke(() =>
        {
            ISelectVisual[] visuals = CreateOverlappingVisuals(useFractionalBounds);
            Rect unionRect = visuals.Select(visual => visual.GetRect()).Aggregate(Rect.Union);
            Int32Rect cropRect = new(
                (int)Math.Floor(unionRect.X),
                (int)Math.Floor(unionRect.Y),
                (int)Math.Ceiling(unionRect.Width),
                (int)Math.Ceiling(unionRect.Height));

            BitmapSource expected = RenderLegacyFullCanvas(visuals, 80, 64, cropRect);
            BitmapSource actual = InvokeRoiRenderer(visuals, 80, 64, cropRect);

            AssertBitmapsEqual(expected, actual);
        });
    }

    [Fact]
    public void EmptyCropRectStillRendersTheWholeCanvas()
    {
        WpfTestHost.Invoke(() =>
        {
            ISelectVisual[] visuals = CreateOverlappingVisuals(useFractionalBounds: false);

            BitmapSource expected = RenderLegacyFullCanvas(visuals, 48, 36, Int32Rect.Empty);
            BitmapSource actual = InvokeRoiRenderer(visuals, 48, 36, Int32Rect.Empty);

            AssertBitmapsEqual(expected, actual);
        });
    }

    [Fact]
    public void LargeCanvasWithSmallSelectionCreatesOnlySelectionSizedBitmap()
    {
        WpfTestHost.Invoke(() =>
        {
            Rect rect = new(70_000, 60_000, 7, 5);
            ISelectVisual[] visuals = [CreateVisual(rect, Colors.LimeGreen, 192)];

            BitmapSource actual = InvokeRoiRenderer(visuals, 100_000, 80_000, new Int32Rect(70_000, 60_000, 7, 5));

            Assert.Equal(7, actual.PixelWidth);
            Assert.Equal(5, actual.PixelHeight);
            Assert.Equal(PixelFormats.Pbgra32, actual.Format);
            Assert.Equal(96, actual.DpiX);
            Assert.Equal(96, actual.DpiY);
        });
    }

    [Theory]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    [InlineData(79, 0, 2, 1)]
    [InlineData(0, 63, 1, 2)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    public void InvalidCropMatchesLegacyArgumentException(int x, int y, int width, int height)
    {
        WpfTestHost.Invoke(() =>
        {
            ISelectVisual[] visuals = [CreateVisual(new Rect(2, 2, 8, 8), Colors.Red, 255)];
            Int32Rect cropRect = new(x, y, width, height);

            Assert.Throws<ArgumentException>(() => RenderLegacyFullCanvas(visuals, 80, 64, cropRect));
            Assert.Throws<ArgumentException>(() => InvokeRoiRenderer(visuals, 80, 64, cropRect));
            Assert.Single(visuals);
        });
    }

    private static ISelectVisual[] CreateOverlappingVisuals(bool useFractionalBounds)
    {
        Rect firstRect = useFractionalBounds ? new Rect(8.25, 7.75, 18.5, 14.25) : new Rect(8, 7, 18, 14);
        Rect secondRect = useFractionalBounds ? new Rect(18.5, 12.125, 20.25, 18.75) : new Rect(18, 12, 20, 18);
        TestSelectVisual first = CreateVisual(firstRect, Colors.OrangeRed, 176);
        TestSelectVisual second = CreateVisual(secondRect, Colors.RoyalBlue, 144);

        // RasterizeSelectionAndReplace historically draws Drawing only; these Visual properties are intentionally ignored.
        second.Transform = new TranslateTransform(500, 500);
        second.Offset = new Vector(200, 200);
        second.Clip = new RectangleGeometry(new Rect(500, 500, 1, 1));
        return [first, second];
    }

    private static TestSelectVisual CreateVisual(Rect rect, Color color, byte alpha)
    {
        TestSelectVisual visual = new(rect);
        using DrawingContext context = visual.RenderOpen();
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), null, rect);
        return visual;
    }

    private static CroppedBitmap RenderLegacyFullCanvas(IEnumerable<ISelectVisual> visuals, int canvasWidth, int canvasHeight, Int32Rect cropRect)
    {
        RenderTargetBitmap fullCanvas = new(canvasWidth, canvasHeight, 96, 96, PixelFormats.Pbgra32);
        DrawingVisual composite = new();
        using (DrawingContext context = composite.RenderOpen())
        {
            foreach (DrawingVisual visual in visuals.OfType<DrawingVisual>())
                context.DrawDrawing(visual.Drawing);
        }
        fullCanvas.Render(composite);
        return new CroppedBitmap(fullCanvas, cropRect);
    }

    private static BitmapSource InvokeRoiRenderer(IEnumerable<ISelectVisual> visuals, int canvasWidth, int canvasHeight, Int32Rect cropRect)
    {
        MethodInfo method = typeof(SelectEditorVisual).GetMethod("RasterizeSelection", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(SelectEditorVisual).FullName, "RasterizeSelection");
        try
        {
            return Assert.IsAssignableFrom<BitmapSource>(method.Invoke(null, [visuals, canvasWidth, canvasHeight, cropRect]));
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static void AssertBitmapsEqual(BitmapSource expected, BitmapSource actual)
    {
        Assert.Equal(expected.PixelWidth, actual.PixelWidth);
        Assert.Equal(expected.PixelHeight, actual.PixelHeight);
        Assert.Equal(expected.DpiX, actual.DpiX);
        Assert.Equal(expected.DpiY, actual.DpiY);
        Assert.Equal(expected.Format, actual.Format);

        int stride = (expected.PixelWidth * expected.Format.BitsPerPixel + 7) / 8;
        byte[] expectedPixels = new byte[stride * expected.PixelHeight];
        byte[] actualPixels = new byte[stride * actual.PixelHeight];
        expected.CopyPixels(expectedPixels, stride, 0);
        actual.CopyPixels(actualPixels, stride, 0);
        Assert.Equal(expectedPixels, actualPixels);
    }

    private sealed class TestSelectVisual : DrawingVisual, ISelectVisual
    {
        private Rect rect;

        internal TestSelectVisual(Rect rect)
        {
            this.rect = rect;
        }

        public Rect GetRect() => rect;

        public void SetRect(Rect value) => rect = value;
    }
}
