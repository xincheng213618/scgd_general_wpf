using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.DistortionP9;
using ColorVision.Core;
using FindLightBeadsCommand = ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FindLightBeads.FindLightBeads;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmCircleOverlayRenderOptimizationTests
{
    [Fact]
    public void FindLightBeadsCreatesOneRenderedCircleWithZoomAdjustedStroke()
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new() { IsLayoutUpdated = false };
            Zoombox zoombox = new()
            {
                Child = canvas,
                ContentMatrix = new Matrix(2, 0, 0, 2, 0, 0),
            };
            DrawEditorContext context = new(canvas, zoombox);
            FindLightBeadsCommand command = new(null!, context);

            MethodInfo addCircle = typeof(FindLightBeadsCommand).GetMethod(
                "AddCircleOverlay",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            addCircle.Invoke(command, [new Point(40, 50), 12d, Brushes.Red]);

            DVCircle circle = Assert.IsType<DVCircle>(Assert.Single(canvas.Visuals));
            Assert.Equal(new Point(40, 50), circle.Center);
            Assert.Equal(12, circle.Radius);
            Assert.Equal(12, circle.Attribute.RadiusY);
            Assert.Equal(0.5, circle.Pen.Thickness);
            Assert.Equal(Colors.Red, GetColor(circle.Pen.Brush));
            Assert.Equal(Colors.Transparent, GetColor(circle.Attribute.Brush));
            Assert.NotNull(circle.Drawing);
            Assert.Single(canvas.UndoStack);
        });
    }

    [Fact]
    public void DistortionP9CreatesOneRenderedLabeledCircleWithLegacyFontSize()
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new() { IsLayoutUpdated = false };
            Zoombox zoombox = new()
            {
                Child = canvas,
                ContentMatrix = Matrix.Identity,
            };
            DrawEditorContext context = new(canvas, zoombox);
            DistortionP9Point point = new() { Id = 4, Name = "P4", X = 100, Y = 80 };
            Pen pen = new(Brushes.OrangeRed, 1);

            Type runner = typeof(DistortionP9Point).Assembly.GetType(
                "ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.DistortionP9.DistortionP9AnalysisRunner",
                throwOnError: true)!;
            MethodInfo addCircle = runner.GetMethod(
                "AddCircle",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            addCircle.Invoke(null, [context, point, 15d, pen, Brushes.Gold]);

            DVCircleText circle = Assert.IsType<DVCircleText>(Assert.Single(canvas.Visuals));
            Assert.Equal(new Point(100, 80), circle.Center);
            Assert.Equal(15, circle.Radius);
            Assert.Equal(15, circle.Attribute.RadiusY);
            Assert.Same(pen, circle.Pen);
            Assert.Equal(Colors.OrangeRed, GetColor(circle.Pen.Brush));
            Assert.Equal(Colors.Transparent, GetColor(circle.Attribute.Brush));
            Assert.Equal("P4", circle.Attribute.Text);
            Assert.Equal(Colors.Gold, GetColor(circle.Attribute.Foreground));
            Assert.Equal(20, circle.TextAttribute.FontSize);
            Assert.NotNull(circle.Drawing);
            Assert.Single(canvas.UndoStack);
        });
    }

    [Theory]
    [InlineData(0d, 1d)]
    [InlineData(-2d, 0.5d)]
    [InlineData(double.NaN, 1d)]
    [InlineData(double.PositiveInfinity, 1d)]
    [InlineData(double.Epsilon, 1d)]
    public void FindLightBeadsNormalizesInvalidZoomBeforeCreatingPen(double zoom, double expectedThickness)
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new() { IsLayoutUpdated = false };
            Zoombox zoombox = new()
            {
                Child = canvas,
                ContentMatrix = new Matrix(zoom, 0, 0, zoom, 0, 0),
            };
            DrawEditorContext context = new(canvas, zoombox);
            FindLightBeadsCommand command = new(null!, context);

            GetPrivateMethod(typeof(FindLightBeadsCommand), "AddCircleOverlay")
                .Invoke(command, [new Point(10, 20), 8d, Brushes.Yellow]);

            DVCircle circle = Assert.IsType<DVCircle>(Assert.Single(canvas.Visuals));
            Assert.Equal(expectedThickness, circle.Pen.Thickness);
            Assert.True(double.IsFinite(circle.Pen.Thickness));
            Assert.NotNull(circle.Drawing);
            Assert.Single(canvas.UndoStack);
        });
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.Epsilon)]
    public void DistortionP9SkipsInvalidPointsAndUsesFiniteFallbackStyle(double zoom)
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new() { IsLayoutUpdated = false };
            Zoombox zoombox = new()
            {
                Child = canvas,
                ContentMatrix = new Matrix(zoom, 0, 0, zoom, 0, 0),
            };
            DrawEditorContext context = new(canvas, zoombox);
            DistortionP9NativeResult result = new()
            {
                Success = true,
                CandidatePoints =
                [
                    new DistortionP9Point { Id = 9, X = 5, Y = 6 },
                    new DistortionP9Point { Id = 10, X = double.NaN, Y = 7 },
                ],
                Points =
                [
                    new DistortionP9Point { Id = 0, Row = 0, Col = 0, X = 10, Y = 20 },
                    new DistortionP9Point { Id = 1, Row = 0, Col = 1, X = 30, Y = 20 },
                    new DistortionP9Point { Id = 2, Row = 0, Col = 2, X = double.PositiveInfinity, Y = 20 },
                ],
            };

            Type runner = GetDistortionRunnerType();
            GetPrivateMethod(runner, "DrawResultOverlay").Invoke(null, [result, context]);

            Assert.Collection(
                canvas.Visuals,
                visual => AssertCircle(visual, new Point(5, 6), 15, 1.2),
                visual =>
                {
                    DVLine line = Assert.IsType<DVLine>(visual);
                    Assert.Equal([new Point(10, 20), new Point(30, 20)], line.Points);
                    Assert.Equal(1, line.Pen.Thickness);
                    Assert.True(double.IsFinite(line.Pen.Thickness));
                    Assert.NotNull(line.Drawing);
                },
                visual => AssertCircle(visual, new Point(10, 20), 20, 1.5),
                visual => AssertCircle(visual, new Point(30, 20), 20, 1.5));
            Assert.Equal(4, canvas.UndoStack.Count);
        });
    }

    [Fact]
    public void DistortionP9RoiIntersectionDoesNotOverflowIntEndpoints()
    {
        Type runner = GetDistortionRunnerType();
        MethodInfo normalize = GetPrivateMethod(runner, "TryNormalizeRoi");
        object?[] arguments =
        [
            new RoiRect(int.MaxValue - 10, 5, 100, 10),
            new HImage { cols = int.MaxValue, rows = 100 },
            null,
        ];

        Assert.True((bool)normalize.Invoke(null, arguments)!);
        RoiRect roi = Assert.IsType<RoiRect>(arguments[2]);
        Assert.Equal(int.MaxValue - 10, roi.X);
        Assert.Equal(5, roi.Y);
        Assert.Equal(10, roi.Width);
        Assert.Equal(10, roi.Height);
    }

    private static void AssertCircle(object visual, Point center, double radius, double thickness)
    {
        DVCircleText circle = Assert.IsType<DVCircleText>(visual);
        Assert.Equal(center, circle.Center);
        Assert.Equal(radius, circle.Radius);
        Assert.Equal(thickness, circle.Pen.Thickness, precision: 10);
        Assert.True(double.IsFinite(circle.Pen.Thickness));
        Assert.NotNull(circle.Drawing);
    }

    private static Type GetDistortionRunnerType()
    {
        return typeof(DistortionP9Point).Assembly.GetType(
            "ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.DistortionP9.DistortionP9AnalysisRunner",
            throwOnError: true)!;
    }

    private static MethodInfo GetPrivateMethod(Type type, string name)
    {
        return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!;
    }

    private static Color GetColor(Brush brush)
        => Assert.IsType<SolidColorBrush>(brush).Color;
}
