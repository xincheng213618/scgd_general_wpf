using ColorVision.Engine.Media;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Annotations;
using ColorVision.ImageEditor;
using System.Reflection;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class CvcieRegionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedPolygonAndLassoStayClosedAcrossUndoRedo(bool lasso)
    {
        WpfTestHost.Invoke(() =>
        {
            using var canvas = new DrawCanvas();
            var zoom = new Zoombox { Child = canvas, ContentMatrix = Matrix.Identity };
            var context = new DrawEditorContext(canvas, zoom);
            using var selection = new SelectEditorVisual(context);
            context.SelectionVisual = selection;
            using PolygonManager manager = lasso ? new LassoManager(context) : new PolygonManager(context);
            manager.IsChecked = true;
            var toolType = typeof(MultiPointDrawingToolBase<DVPolygon>);
            toolType.GetMethod("HandlePreviewMouseLeftButtonDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager,
                [canvas, new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent }]);
            var polygon = Assert.Single(canvas.Visuals.OfType<DVPolygon>());
            polygon.Points.Clear();
            polygon.Points.AddRange([new(0, 0), new(20, 0), new(10, 10)]);
            toolType.GetMethod("CompleteCurrentVisual", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, [false]);
            manager.IsChecked = false;
            Assert.True(polygon.Attribute.IsClosed);
            Assert.Single(canvas.UndoStack);
            canvas.Undo();
            Assert.False(canvas.ContainsVisual(polygon));
            canvas.Redo();
            Assert.True(canvas.ContainsVisual(polygon));
            Assert.True(polygon.Attribute.IsClosed);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(-45)]
    public void EllipseRunsMatchInverseRotatedPixelMembership(double degrees)
    {
        Point center = new(8.2, 7.3);
        var region = ClosedPixelRegion.Ellipse(center, 6.2, 2.1, degrees);
        var actual = Pixels(region, 20, 20);
        var expected = new HashSet<(int, int)>();
        double angle = degrees * Math.PI / 180, c = Math.Cos(angle), s = Math.Sin(angle);
        for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
            {
                double dx = x - center.X, dy = y - center.Y;
                double localX = c * dx + s * dy, localY = -s * dx + c * dy;
                if (localX * localX / (6.2 * 6.2) + localY * localY / (2.1 * 2.1) < 1) expected.Add((x, y));
            }
        Assert.True(expected.SetEquals(actual));
    }

    [Fact]
    public void ConcavePolygonClipsToImageAndDoesNotUseBoundingRectangle()
    {
        var region = ClosedPixelRegion.Polygon(new Point[] { new(-2, -2), new(6, -2), new(6, 2), new(2, 2), new(2, 6), new(-2, 6) });
        var actual = Pixels(region, 8, 8);
        Assert.Equal(20, actual.Count);
        Assert.Contains((1, 5), actual);
        Assert.Contains((5, 1), actual);
        Assert.DoesNotContain((3, 3), actual);
    }

    [Fact]
    public void EqualAxisEllipsePreservesLegacyCircleMeasurement()
    {
        float[] values = Enumerable.Range(0, 3 * 21 * 21).Select(i => (float)(i * i % 997)).ToArray();
        using var buffer = Buffer(values, 21, 21, 3);
        for (int diameter = 1; diameter <= 16; diameter++)
        {
            var circle = new PoiMeasurementPoint(10, 10, diameter, diameter, PoiMeasurementShape.Circle);
            Assert.Equal(PoiMeasurementService.CalculateRaw(buffer, new[] { circle })[0],
                PoiMeasurementService.CalculateRaw(buffer, new[] { circle with { Shape = PoiMeasurementShape.Ellipse } })[0]);
        }
    }

    [Fact]
    public void RegionAveragesXyzBeforeColorConversionAndPreservesNegativePolicy()
    {
        using var buffer = Buffer(new float[] { -2, -4, 100, 100, 3, 5, 100, 100, 6, 8, 100, 100 }, 2, 2, 3);
        var region = ClosedPixelRegion.Polygon(new Point[] { new(0, 0), new(2, 0), new(2, 1), new(0, 1) });
        var raw = PoiMeasurementService.CalculateRegion(buffer, region, true);
        Assert.Equal(-3, raw.X);
        Assert.Equal(4, raw.Y);
        Assert.Equal(7, raw.Z);
        Assert.Equal(-0.375f, raw.ChromaX);
        Assert.True(PoiMeasurementService.CalculateRegion(buffer, region, false).X > 0);
        Assert.Throws<ArgumentException>(() => PoiMeasurementService.CalculateRegion(buffer,
            ClosedPixelRegion.Ellipse(new(100, 100), 1, 1), true));
    }

    [Fact]
    public void ProbeUsesSameEllipseDimensionsAndDefaultsCopy()
    {
        var settings = new CvcieMouseProbeOptions { MagnigifierType = MagnigifierType.Ellipse, RectWidth = 12, RectHeight = 6 };
        var copy = new CvcieMouseProbeOptions();
        copy.CopyFrom(settings);
        Assert.Equal(new PoiMeasurementPoint(5, 8, 12, 6, PoiMeasurementShape.Ellipse), copy.CreateMeasurementPoint(5, 8));
    }

    [Fact]
    public void PolygonClosureAndRotationSurviveBothAnnotationPaths()
    {
        WpfTestHost.Invoke(() =>
        {
            var properties = new PolygonProperties { IsClosed = true, Rotation = 30, Points = new() { new(0, 0), new(20, 0), new(0, 10) } };
            var visual = new DVPolygon(properties);
            foreach (var item in new[] { AnnotationMapper.ToItem(properties), AnnotationMapper.ToItem(visual) })
            {
                var restored = Assert.IsType<DVPolygon>(AnnotationMapper.ToVisual(item!));
                Assert.True(restored.IsComple);
                Assert.Equal(30, restored.Attribute.Rotation);
                Assert.Equal(visual.GetRect(), restored.GetRect());
            }
        });
    }

    [Fact]
    public void RotationAndResizeInvalidateOnlyMeasurementMessages()
    {
        WpfTestHost.Invoke(() =>
        {
            var visual = new DVCircleText(new CircleTextProperties { Center = new(50, 50), Radius = 20, RadiusY = 10, Rotation = 30, Text = "Point_1", Msg = "result", IsMeasurementMessage = true });
            visual.SetRect(new Rect(visual.GetRect().X + 10, visual.GetRect().Y, visual.GetRect().Width, visual.GetRect().Height));
            Assert.Equal(60, visual.Attribute.Center.X, 8);
            Assert.Equal(20, visual.Attribute.Radius, 8);
            Assert.Equal(10, visual.Attribute.RadiusY, 8);
            Assert.Null(visual.Attribute.Msg);
            Assert.Equal("Point_1", visual.Attribute.Text);
            visual.Attribute.Msg = "manual";
            visual.Attribute.Rotation = 60;
            Assert.Equal("manual", visual.Attribute.Msg);
        });
    }

    [Fact]
    public void ResultPrecisionDoesNotChangeCsvBytes()
    {
        string file = Path.GetTempFileName();
        int previous = CVCIEShowConfig.Instance.DecimalPlaces;
        try
        {
            var items = new ObservableCollection<PoiResultCIExyuvData> { new() { Point = new PoiPoint { Name = "test", PointType = PoiShape.Rect, PixelX = 2, PixelY = 3, Width = 4, Height = 6 }, X = 1.23456789, Y = 2.34567891, Z = 3.45678912, x = 0.12345678, y = 0.23456789, u = 0.12345, v = 0.34567, CCT = 4567.12345, Wave = 523.45678 } };
            CVCIEShowConfig.Instance.DecimalPlaces = 3;
            items.SaveCsv(file);
            byte[] first = File.ReadAllBytes(file);
            CVCIEShowConfig.Instance.DecimalPlaces = 8;
            items.SaveCsv(file);
            Assert.Equal(first, File.ReadAllBytes(file));
        }
        finally { CVCIEShowConfig.Instance.DecimalPlaces = previous; File.Delete(file); }
    }

    private static HashSet<(int, int)> Pixels(ClosedPixelRegion region, int width, int height)
    {
        var pixels = new HashSet<(int, int)>();
        foreach (var run in region.GetRuns(width, height))
            for (int x = run.StartX; x < run.EndX; x++) Assert.True(pixels.Add((x, run.Y)));
        return pixels;
    }

    private static PoiMeasurementBuffer Buffer(float[] values, int width, int height, int channels)
    {
        byte[] bytes = new byte[values.Length * sizeof(float)];
        System.Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return new(bytes, width, height, 32, channels);
    }
}
