using ColorVision.Engine;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Media;
using AlgorithmPoiShape = CVCommCore.CVAlgorithm.POIPointTypes;
using FlowPoiShape = FlowEngineLib.Node.POI.POIPointTypes;

namespace ColorVision.UI.Tests;

public class PoiPointModelTests
{
    [Theory]
    [InlineData(GraphicTypes.Circle, PoiShape.Circle)]
    [InlineData(GraphicTypes.Rect, PoiShape.Rect)]
    [InlineData(GraphicTypes.Quadrilateral, PoiShape.Quadrilateral)]
    [InlineData(GraphicTypes.Point, PoiShape.Point)]
    [InlineData(GraphicTypes.Polygon, PoiShape.Polygon)]
    public void ConvertsLegacyTemplateShapesAtBoundary(GraphicTypes source, PoiShape expected)
    {
        Assert.Equal(expected, source.ToPoiShape());
        Assert.Equal(source, expected.ToGraphicType());
    }

    [Theory]
    [InlineData(AlgorithmPoiShape.SolidPoint, PoiShape.Point)]
    [InlineData(AlgorithmPoiShape.Circle, PoiShape.Circle)]
    [InlineData(AlgorithmPoiShape.Rect, PoiShape.Rect)]
    [InlineData(AlgorithmPoiShape.LTRect, PoiShape.LeftTopRect)]
    [InlineData(AlgorithmPoiShape.PolygonFour, PoiShape.Quadrilateral)]
    [InlineData(AlgorithmPoiShape.Polygon, PoiShape.Polygon)]
    public void ConvertsAlgorithmShapesAtBoundary(AlgorithmPoiShape source, PoiShape expected)
    {
        Assert.Equal(expected, source.ToPoiShape());
    }

    [Theory]
    [InlineData(FlowPoiShape.SolidPoint, PoiShape.Point)]
    [InlineData(FlowPoiShape.Circle, PoiShape.Circle)]
    [InlineData(FlowPoiShape.Rect, PoiShape.Rect)]
    public void ConvertsFlowShapesAtBoundary(FlowPoiShape source, PoiShape expected)
    {
        Assert.Equal(expected, source.ToPoiShape());
    }

    [Fact]
    public void ReadsLegacyTemplateAndResultCoordinateNames()
    {
        PoiPoint templatePoint = JsonConvert.DeserializeObject<PoiPoint>("""{"PointType":1,"PixX":12.5,"PixY":13.5,"PixWidth":20,"PixHeight":10}""")!;
        PoiPoint resultPoint = JsonConvert.DeserializeObject<PoiPoint>("""{"PointType":0,"PixelX":22.5,"PixelY":23.5,"Width":30,"Height":30}""")!;
        PoiPoint legacySolidPoint = JsonConvert.DeserializeObject<PoiPoint>("""{"PointType":-1,"PixelX":2,"PixelY":3,"Width":1,"Height":1}""")!;

        Assert.Equal(new[] { 12.5, 13.5, 20, 10 }, new[] { templatePoint.PixelX, templatePoint.PixelY, templatePoint.Width, templatePoint.Height });
        Assert.Equal(new[] { 22.5, 23.5, 30, 30 }, new[] { resultPoint.PixX, resultPoint.PixY, resultPoint.PixWidth, resultPoint.PixHeight });
        Assert.Equal(PoiShape.LegacySolidPoint, legacySolidPoint.PointType);
    }

    [Fact]
    public void CreatesCircleRectangleAndPointVisualsFromOneModel()
    {
        WpfTestHost.Invoke(() =>
        {
            PoiOverlayStyle style = new() { Stroke = Brushes.Blue, StrokeThickness = 2, FontSize = 12, PointRadius = 4 };
            PoiPoint circlePoint = new(7, -1, "C7", PoiShape.Circle, 100, 80, 20, 20);
            PoiPoint rectanglePoint = new(8, -1, "R8", PoiShape.Rect, 50, 40, 30, 10);
            PoiPoint solidPoint = new(9, -1, "P9", PoiShape.Point, 12, 14, 1, 1);

            DVCircleText circle = Assert.IsType<DVCircleText>(PoiOverlayRenderer.CreateVisual(circlePoint, "value", style));
            DVRectangleText rectangle = Assert.IsType<DVRectangleText>(PoiOverlayRenderer.CreateVisual(rectanglePoint, style: style));
            DVCircle point = Assert.IsType<DVCircle>(PoiOverlayRenderer.CreateVisual(solidPoint, style: style));

            Assert.Equal(new Point(100, 80), circle.Attribute.Center);
            Assert.Equal(10, circle.Attribute.Radius);
            Assert.Equal("value", circle.Attribute.Msg);
            Assert.Equal(12, circle.Attribute.FontSize);
            Assert.NotNull(circle.Drawing);
            Assert.Equal(new Rect(35, 35, 30, 10), rectangle.Attribute.Rect);
            Assert.Equal(12, rectangle.Attribute.FontSize);
            Assert.NotNull(rectangle.Drawing);
            Assert.Equal(4, point.Attribute.Radius);
            Assert.NotNull(point.Drawing);
            Assert.Same(circlePoint, circle.BaseAttribute.Tag);
        });
    }

    [Fact]
    public void AddRangePreparesVisualsAtTheFinalCanvasScaleAndRaisesOneBatchEvent()
    {
        WpfTestHost.Invoke(() =>
        {
            Application application = Application.Current ?? new Application();
            application.Resources["TextBox.Small"] = new Style(typeof(System.Windows.Controls.TextBox));
            application.Resources["ComboBox.Small"] = new Style(typeof(System.Windows.Controls.ComboBox));
            application.Resources["ToolBarBaseStyle"] = new Style(typeof(System.Windows.Controls.ToolBar));
            application.Resources["ToolBarImage"] = new Style(typeof(System.Windows.Controls.Image));
            application.Resources["BaseStyle"] = new Style(typeof(System.Windows.Controls.Control));
            application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            application.Resources["bool2VisibilityConverter"] = new System.Windows.Controls.BooleanToVisibilityConverter();
            using ImageView imageView = new();
            imageView.ImageShow.IsLayoutUpdated = true;
            imageView.ImageShow.Scale = 2.5;
            int initialVisualCount = imageView.ImageShow.Visuals.Count;
            PoiPoint[] points =
            [
                new PoiPoint(1, -1, "C1", PoiShape.Circle, 100, 80, 20, 20),
                new PoiPoint(2, -1, "R2", PoiShape.Rect, 50, 40, 30, 10),
                new PoiPoint(3, -1, "P3", PoiShape.Point, 12, 14, 1, 1),
            ];
            int addEventCount = 0;
            VisualChangedEventArgs? addEvent = null;
            imageView.ImageShow.VisualsAdd += (_, e) =>
            {
                addEventCount++;
                addEvent = e;
            };

            int added = PoiOverlayRenderer.AddRange(imageView, points, point => $"value-{point.Id}");

            Assert.Equal(3, added);
            Assert.Equal(initialVisualCount + 3, imageView.ImageShow.Visuals.Count);
            Assert.Equal(1, addEventCount);
            Assert.NotNull(addEvent);
            Assert.Equal(VisualChangeType.AddRange, addEvent.ChangeType);
            Assert.Equal(3, addEvent.Visuals.Count);

            DVCircleText circle = Assert.IsType<DVCircleText>(imageView.ImageShow.Visuals[initialVisualCount]);
            DVRectangleText rectangle = Assert.IsType<DVRectangleText>(imageView.ImageShow.Visuals[initialVisualCount + 1]);
            DVCircle point = Assert.IsType<DVCircle>(imageView.ImageShow.Visuals[initialVisualCount + 2]);
            Assert.Equal(2.5, circle.Pen.Thickness);
            Assert.Equal(25, circle.TextAttribute.FontSize);
            Assert.Equal("value-1", circle.Attribute.Msg);
            Assert.Equal(2.5, rectangle.Pen.Thickness);
            Assert.Equal(25, rectangle.TextAttribute.FontSize);
            Assert.Equal("value-2", rectangle.Attribute.Msg);
            Assert.Equal(2.5, point.Pen.Thickness);
            Assert.NotNull(circle.Drawing);
            Assert.NotNull(rectangle.Drawing);
            Assert.NotNull(point.Drawing);
            Assert.Same(points[0], circle.BaseAttribute.Tag);
            Assert.Same(points[1], rectangle.BaseAttribute.Tag);
            Assert.Same(points[2], point.BaseAttribute.Tag);
        });
    }

    [Fact]
    public void DefaultBrushIsFrozenForCrossDispatcherUse()
    {
        Assert.True(BaseProperties.DefaultBrush.IsFrozen);
    }
}
