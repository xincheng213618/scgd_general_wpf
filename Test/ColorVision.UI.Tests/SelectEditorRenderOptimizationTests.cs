using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class SelectEditorRenderOptimizationTests
{
    [Fact]
    public void SelectionHandlesInteriorAndExteriorKeepTheirHitBehavior()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
            SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
            TestSelectVisual selected = new(new Rect(30, 50, 100, 80));
            editor.SelectVisuals.Add(selected);
            editor.Render();

            Point[] handles =
            [
                new(30, 50), new(130, 50), new(30, 130), new(130, 130),
                new(80, 50), new(80, 130), new(30, 90), new(130, 90),
            ];
            foreach (Point handle in handles)
                Assert.True(editor.GetContainingRect(handle));

            Assert.True(editor.GetContainingRect(new Point(80, 90)));
            Assert.False(editor.GetContainingRect(new Point(10, 10)));
            editor.Dispose();
        });
    }

    [Fact]
    public void SelectionRenderFallsBackWhenZoomMatrixIsNotReady()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = new Matrix(0, 0, 0, 0, 0, 0) };
            SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
            TestSelectVisual selected = new(new Rect(30, 50, 100, 80));
            editor.SelectVisuals.Add(selected);

            editor.Render();

            Assert.True(editor.GetContainingRect(new Point(30, 50)));
            editor.Dispose();
        });
    }

    [Fact]
    public void LargeSelectionsUseSingleUnionHandle()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
            SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
            TestSelectVisual selected = new(new Rect(40, 40, 10, 10));
            editor.SelectVisuals.Add(selected);
            editor.SelectVisuals.Add(new TestSelectVisual(new Rect(0, 0, 10, 10)));
            for (int i = 0; i < 29; i++)
                editor.SelectVisuals.Add(new TestSelectVisual(new Rect(100 + i * 20, 100, 10, 10)));

            editor.Render();
            Assert.True(editor.GetContainingRect(new Point(40, 40)));
            Assert.Same(selected, editor.ISelectVisual);

            editor.SelectVisuals.Add(new TestSelectVisual(new Rect(700, 100, 10, 10)));
            editor.Render();
            Assert.True(editor.GetContainingRect(new Point(40, 40)));
            Assert.Null(editor.ISelectVisual);
            Assert.Equal(Cursors.SizeAll, zoombox.Cursor);
            editor.Dispose();
        });
    }

    [Fact]
    public void LayoutUpdatesReuseOneTimerAndIgnoreEmptySelections()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
            SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
            FieldInfo timerField = typeof(SelectEditorVisual).GetField("_layoutRenderTimer", BindingFlags.Instance | BindingFlags.NonPublic)!;
            MethodInfo layoutUpdated = typeof(SelectEditorVisual).GetMethod("ZoomboxSub_LayoutUpdated", BindingFlags.Instance | BindingFlags.NonPublic)!;
            DispatcherTimer timer = Assert.IsType<DispatcherTimer>(timerField.GetValue(editor));

            editor.SetRenders(Array.Empty<TestSelectVisual>());
            layoutUpdated.Invoke(editor, [zoombox, EventArgs.Empty]);
            Assert.False(timer.IsEnabled);

            editor.SetRender(new TestSelectVisual(new Rect(20, 20, 40, 30)));
            layoutUpdated.Invoke(editor, [zoombox, EventArgs.Empty]);
            layoutUpdated.Invoke(editor, [zoombox, EventArgs.Empty]);
            Assert.True(timer.IsEnabled);
            Assert.Same(timer, timerField.GetValue(editor));

            editor.ClearRender();
            Assert.False(timer.IsEnabled);
            editor.Dispose();
        });
    }

    [Fact]
    public void SelectionRenderQueriesLongBrushBoundsOnce()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
            SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
            BrushStrokeProperties properties = new() { Pen = new Pen(Brushes.Red, 4) };
            for (int index = 0; index < 20_000; index++)
            {
                properties.Points.Add(new Point(index * 0.25, 40 + index % 7));
            }
            CountingBoundsBrushStroke stroke = new(properties);
            editor.SelectVisuals.Add(stroke);

            editor.Render();

            Assert.Equal(1, stroke.GetRectCallCount);
            editor.Dispose();
        });
    }

    [Fact]
    public void ClearingSelectionReleasesCachedHandleTargets()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
            SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
            TestSelectVisual selected = new(new Rect(30, 50, 100, 80));
            editor.SetRender(selected);

            Assert.True(editor.GetContainingRect(new Point(27, 47)));
            Assert.Same(selected, editor.ISelectVisual);

            editor.ClearRender();

            Assert.False(editor.GetContainingRect(new Point(27, 47)));
            Assert.Null(editor.ISelectVisual);
            editor.Dispose();
        });
    }

    [Fact]
    public void RenderingDuringResizeKeepsTheInteractionTargetUntilSelectionIsCleared()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
            SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
            TestSelectVisual selected = new(new Rect(30, 50, 100, 80));
            editor.SetRender(selected);

            Assert.True(editor.GetContainingRect(new Point(130, 130)));
            Assert.Same(selected, editor.ISelectVisual);
            FieldInfo mouseDownField = typeof(SelectEditorVisual).GetField("IsMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
            mouseDownField.SetValue(editor, true);

            selected.SetRect(new Rect(30, 50, 120, 100));
            editor.Render();
            Assert.Same(selected, editor.ISelectVisual);

            selected.SetRect(new Rect(30, 50, 140, 120));
            editor.Render();
            Assert.Same(selected, editor.ISelectVisual);

            editor.ClearRender();
            Assert.Null(editor.ISelectVisual);
            editor.Dispose();
        });
    }

    [Fact]
    public void UndoingASelectedVisualClearsSelectionState()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
            SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
            TestSelectVisual selected = new(new Rect(30, 50, 100, 80));
            canvas.AddVisualCommand(selected);
            editor.SetRender(selected);

            Assert.Single(editor.SelectVisuals);
            Assert.True(editor.GetContainingRect(new Point(30, 50)));

            canvas.Undo();

            Assert.False(canvas.ContainsVisual(selected));
            Assert.Empty(editor.SelectVisuals);
            Assert.Null(editor.ISelectVisual);
            Assert.False(editor.GetContainingRect(new Point(30, 50)));
            editor.Dispose();
        });
    }

    [Fact]
    public void DisposedSelectionEditorCanBeCollectedWhileCanvasStaysAlive()
    {
        WpfTestHost.Invoke(() =>
        {
            (DrawCanvas canvas, Zoombox zoombox, WeakReference editorReference) = CreateDisposedEditor();

            ForceFullCollection();

            Assert.False(editorReference.IsAlive);
            Assert.Empty(canvas.Visuals);
            GC.KeepAlive(canvas);
            GC.KeepAlive(zoombox);
        });
    }

    [Fact]
    public void DisposingDuringMarqueeRemovesTransientVisualAndReleasesInteractionState()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
            SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
            FieldInfo selectionRectField = typeof(SelectEditorVisual).GetField("SelectRect", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo mouseDownField = typeof(SelectEditorVisual).GetField("IsMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
            DrawingVisual selectionRect = Assert.IsType<DrawingVisual>(selectionRectField.GetValue(editor));
            canvas.AddVisual(selectionRect);
            mouseDownField.SetValue(editor, true);
            canvas.CaptureMouse();

            editor.Dispose();

            Assert.False(canvas.ContainsVisual(selectionRect));
            Assert.False(Assert.IsType<bool>(mouseDownField.GetValue(editor)));
            Assert.False(canvas.IsMouseCaptured);
            Assert.Empty(canvas.Visuals);
            canvas.Dispose();
        });
    }

    [Fact]
    public void LineRenderMatchesLegacyPerSegmentPens()
    {
        WpfTestHost.Invoke(() =>
        {
            List<Point> points = [new(24, 40), new(100, 28), new(165, 90), new(230, 45)];
            Pen sourcePen = new(new SolidColorBrush(Color.FromArgb(210, 30, 170, 225)), 3.25);
            DVLine actual = new(new LineProperties { Points = [.. points], Pen = sourcePen });
            actual.Render();

            DrawingVisual expected = RenderLegacyLine(points, sourcePen);

            AssertVisualPixelsEqual(expected, actual, 280, 150);
        });
    }

    [Fact]
    public void PolygonRenderMatchesLegacyPerSegmentPens()
    {
        WpfTestHost.Invoke(() =>
        {
            List<Point> points = [new(35, 30), new(145, 22), new(230, 80), new(175, 140), new(55, 125)];
            Pen sourcePen = new(new SolidColorBrush(Color.FromArgb(220, 210, 75, 55)), 4)
            {
                DashStyle = DashStyles.Dash,
            };
            DVPolygon actual = new(new PolygonProperties { Points = [.. points], Pen = sourcePen }) { IsComple = true };
            actual.Render();

            DrawingVisual expected = RenderLegacyPolygon(points, sourcePen, isComplete: true);

            AssertVisualPixelsEqual(expected, actual, 280, 180);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RulerRenderMatchesLegacyPerSegmentPensAndText(bool includeMovePoint)
    {
        WpfTestHost.Invoke(() =>
        {
            List<Point> points = [new(90, 100), new(190, 120), new(270, 75), new(360, 145)];
            Point? movePoint = includeMovePoint ? new Point(430, 180) : null;
            DrawingVisualRuler actual = new()
            {
                MovePoints = movePoint,
            };
            actual.Attribute.Pen = new Pen(Brushes.DarkGreen, 2);
            actual.Points.AddRange(points);
            actual.Render();

            DrawingVisual expected = RenderLegacyRuler(points, movePoint, actual.Attribute.Pen.Thickness);

            AssertVisualPixelsEqual(expected, actual, 600, 260);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (DrawCanvas Canvas, Zoombox Zoombox, WeakReference EditorReference) CreateDisposedEditor()
    {
        DrawCanvas canvas = new();
        Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
        SelectEditorVisual editor = new(new DrawEditorContext(canvas, zoombox));
        editor.SetRender(new TestSelectVisual(new Rect(20, 20, 40, 30)));
        WeakReference editorReference = new(editor);
        editor.Dispose();
        return (canvas, zoombox, editorReference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ForceFullCollection()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static DrawingVisual RenderLegacyLine(List<Point> points, Pen sourcePen)
    {
        DrawingVisual visual = new();
        using DrawingContext context = visual.RenderOpen();
        for (int i = 1; i < points.Count; i++)
            context.DrawLine(new Pen(sourcePen.Brush, sourcePen.Thickness), points[i - 1], points[i]);
        return visual;
    }

    private static DrawingVisual RenderLegacyPolygon(List<Point> points, Pen sourcePen, bool isComplete)
    {
        DrawingVisual visual = new();
        using DrawingContext context = visual.RenderOpen();
        for (int i = 1; i < points.Count; i++)
            context.DrawLine(new Pen(sourcePen.Brush, sourcePen.Thickness), points[i - 1], points[i]);
        if (isComplete && points.Count > 0)
            context.DrawLine(sourcePen, points[^1], points[0]);
        return visual;
    }

    private static DrawingVisual RenderLegacyRuler(List<Point> points, Point? movePoint, double thickness)
    {
        Brush brush = Brushes.Red;
        Brush moveBrush = Brushes.Pink;
        FontFamily fontFamily = new("Arial");
        double fontSize = thickness * 10;
        DrawingVisual visual = new();
        using DrawingContext context = visual.RenderOpen();

        for (int i = 1; i < points.Count; i++)
            context.DrawLine(new Pen(brush, thickness), points[i - 1], points[i]);
        if (points.Count > 0 && movePoint != null)
            context.DrawLine(new Pen(moveBrush, thickness), points[^1], movePoint.Value);

        if (points.Count == 0)
            return visual;

        FormattedText startText = CreateLegacyRulerText(
            ColorVision.ImageEditor.Properties.Resources.Ruler_StartPoint, fontFamily, fontSize, brush, visual);
        startText.TextAlignment = TextAlignment.Center;
        context.DrawText(startText, points[0]);

        double totalLength = 0;
        for (int i = 1; i < points.Count - 1; i++)
        {
            double length = DrawingVisualRuler.GetDistance(points[i], points[i - 1]) * DrawingVisualRuler.ActualLength;
            totalLength += length;
            context.DrawText(
                CreateLegacyRulerText(length.ToString("F2") + DrawingVisualRuler.PhysicalUnit, fontFamily, fontSize, brush, visual),
                points[i]);
        }

        if (points.Count > 1)
        {
            double lastLength = DrawingVisualRuler.GetDistance(points[^1], points[^2]) * DrawingVisualRuler.ActualLength;
            string text;
            if (movePoint == null)
            {
                totalLength += lastLength;
                text = ColorVision.ImageEditor.Properties.Resources.Ruler_TotalLength + totalLength.ToString("F2") + DrawingVisualRuler.PhysicalUnit;
            }
            else
            {
                text = lastLength.ToString("F2") + DrawingVisualRuler.PhysicalUnit;
            }
            context.DrawText(CreateLegacyRulerText(text, fontFamily, fontSize, brush, visual), points[^1]);
        }

        return visual;
    }

    private static FormattedText CreateLegacyRulerText(string text, FontFamily fontFamily, double fontSize, Brush brush, Visual visual)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(visual).PixelsPerDip);
    }

    private static void AssertVisualPixelsEqual(Visual expected, Visual actual, int width, int height)
    {
        Assert.Equal(RenderPixels(expected, width, height), RenderPixels(actual, width, height));
    }

    private static byte[] RenderPixels(Visual visual, int width, int height)
    {
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        byte[] pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
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

    private sealed class CountingBoundsBrushStroke : DVBrushStroke
    {
        public CountingBoundsBrushStroke(BrushStrokeProperties properties)
            : base(properties)
        {
        }

        public int GetRectCallCount { get; private set; }

        public override Rect GetRect()
        {
            GetRectCallCount++;
            return base.GetRect();
        }
    }
}
