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
            Assert.Equal(new Rect(35, 35, 30, 10), rectangle.Attribute.Rect);
            Assert.Equal(4, point.Attribute.Radius);
            Assert.Same(circlePoint, circle.BaseAttribute.Tag);
        });
    }

    [Fact]
    public void DefaultBrushIsFrozenForCrossDispatcherUse()
    {
        Assert.True(BaseProperties.DefaultBrush.IsFrozen);
    }
}
