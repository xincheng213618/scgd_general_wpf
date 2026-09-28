using ColorVision.ImageEditor;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class DrawCanvasTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangingSourceReleasesPreviousDrawingWithoutAnotherRender(bool replace)
    {
        StaTest.Run(() =>
        {
            BitmapSource original = CreateBitmap();
            using DrawCanvas canvas = new() { Source = original };
            RenderCanvas(canvas);
            DrawingVisual imageVisual = (DrawingVisual)VisualTreeHelper.GetChild(canvas, 0);
            Assert.Same(original, Assert.Single(imageVisual.Drawing.Children.OfType<ImageDrawing>()).ImageSource);

            BitmapSource? replacement = replace ? CreateBitmap() : null;
            canvas.Source = replacement;

            Assert.Same(replacement, canvas.Source);
            Assert.Null(imageVisual.Drawing);
            if (replace)
            {
                RenderCanvas(canvas);
                DrawingGroup drawing = Assert.IsType<DrawingGroup>(imageVisual.Drawing);
                Assert.Same(replacement, Assert.Single(drawing.Children.OfType<ImageDrawing>()).ImageSource);
            }
        });
    }

    [Fact]
    public void ClearingAnnotationsPreservesSourceAndImageDrawing()
    {
        StaTest.Run(() =>
        {
            BitmapSource source = CreateBitmap();
            using DrawCanvas canvas = new() { Source = source };
            canvas.AddVisual(new DrawingVisual());
            RenderCanvas(canvas);
            DrawingVisual imageVisual = (DrawingVisual)VisualTreeHelper.GetChild(canvas, 0);

            canvas.Clear();

            Assert.Empty(canvas.Visuals);
            Assert.Same(source, canvas.Source);
            Assert.Same(source, Assert.Single(imageVisual.Drawing.Children.OfType<ImageDrawing>()).ImageSource);
        });
    }

    [Fact]
    public void DisposeClearsSourceAndRenderedImage()
    {
        StaTest.Run(() =>
        {
            DrawCanvas canvas = new() { Source = CreateBitmap() };
            RenderCanvas(canvas);
            DrawingVisual imageVisual = (DrawingVisual)VisualTreeHelper.GetChild(canvas, 0);
            Assert.NotEmpty(imageVisual.Drawing.Children);

            canvas.Dispose();

            Assert.Null(canvas.Source);
            Assert.Null(imageVisual.Drawing);
        });
    }

    private static BitmapSource CreateBitmap() => new WriteableBitmap(64, 64, 96, 96, PixelFormats.Bgra32, null);

    private static void RenderCanvas(DrawCanvas canvas)
    {
        canvas.Measure(new Size(64, 64));
        canvas.Arrange(new Rect(0, 0, 64, 64));
        canvas.UpdateLayout();
        new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32).Render(canvas);
    }

    [Fact]
    public void ContainsVisualTracksAddAndRemove()
    {
        StaTest.Run(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual visual = new();

            Assert.False(canvas.ContainsVisual(visual));

            canvas.AddVisual(visual);
            Assert.True(canvas.ContainsVisual(visual));

            canvas.RemoveVisual(visual);
            Assert.False(canvas.ContainsVisual(visual));
        });
    }

    [Fact]
    public void FailedLayoutScaleDoesNotPoisonVisualMembership()
    {
        StaTest.Run(() =>
        {
            using DrawCanvas canvas = new();
            ThrowingScaleVisual visual = new();

            Assert.Throws<InvalidOperationException>(() => canvas.AddVisuals([visual]));
            Assert.False(canvas.ContainsVisual(visual));
            Assert.DoesNotContain(visual, canvas.Visuals);

            visual.ShouldThrow = false;
            Assert.Equal(1, canvas.AddVisuals([visual, visual]));
            Assert.True(canvas.ContainsVisual(visual));
            Assert.Equal(new Visual[] { visual }, canvas.Visuals);
        });
    }

    [Fact]
    public void BatchLayoutScaleFailureRollsBackEarlierVisualsWithoutEvents()
    {
        StaTest.Run(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual first = new();
            ThrowingScaleVisual failing = new();
            int addEventCount = 0;
            int changedEventCount = 0;
            canvas.VisualsAdd += (_, _) => addEventCount++;
            canvas.VisualsChanged += (_, _) => changedEventCount++;

            Assert.Throws<InvalidOperationException>(() => canvas.AddVisuals([first, failing]));

            Assert.Empty(canvas.Visuals);
            Assert.False(canvas.ContainsVisual(first));
            Assert.False(canvas.ContainsVisual(failing));
            Assert.Equal(0, addEventCount);
            Assert.Equal(0, changedEventCount);

            failing.ShouldThrow = false;
            Assert.Equal(2, canvas.AddVisuals([first, failing]));
            Assert.Equal(new Visual[] { first, failing }, canvas.Visuals);
            Assert.Equal(1, addEventCount);
            Assert.Equal(1, changedEventCount);
        });
    }

    [Fact]
    public void BatchTopVisualsPreservesExistingOrderingSemantics()
    {
        StaTest.Run(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual first = new();
            DrawingVisual second = new();
            DrawingVisual third = new();
            canvas.AddVisual(first);
            canvas.AddVisual(second);
            canvas.AddVisual(third);

            canvas.BatchTopVisuals([second, first]);

            Assert.Equal(new Visual[] { third, second, first }, canvas.Visuals);
        });
    }

    [Fact]
    public void BatchTopVisualsIgnoresDuplicateInputs()
    {
        StaTest.Run(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual first = new();
            DrawingVisual second = new();
            DrawingVisual third = new();
            canvas.AddVisual(first);
            canvas.AddVisual(second);
            canvas.AddVisual(third);
            int topChangeCount = 0;
            canvas.VisualsChanged += (_, e) =>
            {
                if (e.ChangeType == VisualChangeType.Top)
                    topChangeCount++;
            };

            canvas.BatchTopVisuals([second, second, first, first]);

            Assert.Equal(new Visual[] { third, second, first }, canvas.Visuals);
            Assert.Equal(1, topChangeCount);
        });
    }

    [Fact]
    public void UndoRemoveRestoresOriginalVisualOrder()
    {
        StaTest.Run(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual first = new();
            DrawingVisual second = new();
            DrawingVisual third = new();
            canvas.AddVisual(first);
            canvas.AddVisual(second);
            canvas.AddVisual(third);

            canvas.RemoveVisualCommand(second);
            canvas.Undo();

            Assert.Equal(new Visual[] { first, second, third }, canvas.Visuals);
        });
    }

    [Fact]
    public void ClearingTransientOverlaysPreservesPersistentVisuals()
    {
        StaTest.Run(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual persistent = new();
            DrawingVisual transient = new();
            canvas.AddVisual(persistent);
            canvas.AddOverlayVisual(transient);

            canvas.ClearOverlayVisuals();

            Assert.True(canvas.ContainsVisual(persistent));
            Assert.False(canvas.ContainsVisual(transient));
            Assert.Equal(new Visual[] { persistent }, canvas.Visuals);
        });
    }

    private sealed class ThrowingScaleVisual : DrawingVisual, ILayoutScaleDrawingVisual
    {
        public bool ShouldThrow { get; set; } = true;

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            if (ShouldThrow)
                throw new InvalidOperationException("Synthetic layout-scale failure.");
        }
    }
}
