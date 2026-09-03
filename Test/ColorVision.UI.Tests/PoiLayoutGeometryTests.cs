using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using Newtonsoft.Json;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class PoiLayoutGeometryTests
{
    private static readonly Point[] RectangleCorners =
    [
        new(0, 0),
        new(100, 0),
        new(100, 50),
        new(0, 50),
    ];

    [Fact]
    public void CreateQuadrilateralGrid_GeneratesCenterAndCorners()
    {
        List<Point> points = PoiLayoutGeometry.CreateQuadrilateralGrid(RectangleCorners, 3, 3);

        Assert.Equal(9, points.Count);
        Assert.Equal(new Point(0, 0), points[0]);
        Assert.Equal(new Point(50, 25), points[4]);
        Assert.Equal(new Point(100, 50), points[8]);
    }

    [Fact]
    public void TryNormalizeQuadrilateral_FixesEveryStartAndDirection()
    {
        IEnumerable<Point[]> permutations =
            from first in Enumerable.Range(0, 4)
            from second in Enumerable.Range(0, 4)
            from third in Enumerable.Range(0, 4)
            from fourth in Enumerable.Range(0, 4)
            where new[] { first, second, third, fourth }.Distinct().Count() == 4
            select new[] { RectangleCorners[first], RectangleCorners[second], RectangleCorners[third], RectangleCorners[fourth] };

        foreach (Point[] permutation in permutations)
        {
            Assert.True(PoiLayoutGeometry.TryNormalizeQuadrilateral(permutation, out List<Point> normalized));
            Assert.Equal(RectangleCorners, normalized);
        }
    }

    [Fact]
    public void CreateQuadrilateralGrid_CentersSingletonDimensions()
    {
        List<Point> row = PoiLayoutGeometry.CreateQuadrilateralGrid(RectangleCorners, 1, 3);
        List<Point> column = PoiLayoutGeometry.CreateQuadrilateralGrid(RectangleCorners, 3, 1);
        List<Point> point = PoiLayoutGeometry.CreateQuadrilateralGrid(RectangleCorners, 1, 1);

        Assert.Equal([new Point(0, 25), new Point(50, 25), new Point(100, 25)], row);
        Assert.Equal([new Point(50, 0), new Point(50, 25), new Point(50, 50)], column);
        Assert.Equal([new Point(50, 25)], point);
    }

    [Fact]
    public void TryGetCollapsedPoint_AcceptsFiftyPercentInsetForOneByOneLayout()
    {
        Point[] collapsedCorners = [new(50, 25), new(50, 25), new(50, 25), new(50, 25)];

        Assert.True(PoiLayoutGeometry.TryGetCollapsedPoint(collapsedCorners, out Point point));
        Assert.Equal(new Point(50, 25), point);
        Assert.False(PoiLayoutGeometry.TryGetCollapsedPoint(RectangleCorners, out _));
    }

    [Fact]
    public void TryOffsetForCircle_InsetsAndOutsetsRectangle()
    {
        Assert.True(PoiLayoutGeometry.TryOffsetForCircle(RectangleCorners, 10, DrawingGraphicPosition.Internal, out List<Point> inset));
        Assert.True(PoiLayoutGeometry.TryOffsetForCircle(RectangleCorners, 10, DrawingGraphicPosition.External, out List<Point> outset));

        AssertPointsEqual([new Point(10, 10), new Point(90, 10), new Point(90, 40), new Point(10, 40)], inset);
        AssertPointsEqual([new Point(-10, -10), new Point(110, -10), new Point(110, 60), new Point(-10, 60)], outset);
    }

    [Fact]
    public void TryOffsetForRectangle_UsesAxisAlignedSamplingWindowSupport()
    {
        Point[] skewedCorners =
        [
            new(10, 10),
            new(110, 20),
            new(90, 100),
            new(0, 80),
        ];

        Assert.True(PoiLayoutGeometry.TryOffsetForRectangle(skewedCorners, 20, 10, DrawingGraphicPosition.Internal, out List<Point> inset));

        const double halfWidth = 10;
        const double halfHeight = 5;
        for (int edgeIndex = 0; edgeIndex < skewedCorners.Length; edgeIndex++)
        {
            Point start = skewedCorners[edgeIndex];
            Vector edge = skewedCorners[(edgeIndex + 1) % skewedCorners.Length] - start;
            Vector inwardNormal = new(-edge.Y, edge.X);
            inwardNormal.Normalize();
            double requiredDistance = Math.Abs(inwardNormal.X) * halfWidth + Math.Abs(inwardNormal.Y) * halfHeight;

            foreach (Point point in inset)
            {
                double distance = Vector.Multiply(point - start, inwardNormal);
                Assert.True(distance >= requiredDistance - 1e-8, $"Point {point} is only {distance} px inside edge {edgeIndex}.");
            }
        }
    }

    [Fact]
    public void TryOffsetForCircle_RejectsInsetLargerThanArea()
    {
        Assert.False(PoiLayoutGeometry.TryOffsetForCircle(RectangleCorners, 30, DrawingGraphicPosition.Internal, out _));
    }

    [Fact]
    public void TryOffsetForCircle_RejectsDegenerateLineOnQuadrilateral()
    {
        Point[] degenerateCorners = [new(0, 0), new(50, 0), new(100, 0), new(150, 0)];

        Assert.False(PoiLayoutGeometry.TryOffsetForCircle(degenerateCorners, 10, DrawingGraphicPosition.LineOn, out _));
    }

    [Fact]
    public void PointPosition_DefaultsToInternalAndRoundTrips()
    {
        PoiConfig config = new();
        Assert.Equal(DrawingGraphicPosition.Internal, config.PointPosition);

        config.PointPosition = DrawingGraphicPosition.External;
        PoiConfig restored = JsonConvert.DeserializeObject<PoiConfig>(JsonConvert.SerializeObject(config))!;

        Assert.Equal(DrawingGraphicPosition.External, restored.PointPosition);
    }

    [Theory]
    [InlineData(2, 5, 20, 25)]
    [InlineData(3, 3, 33, 16)]
    [InlineData(1, 1, 99, 49)]
    [InlineData(1, 3, 33, 49)]
    [InlineData(3, 1, 99, 16)]
    public void AutoFitRectangle_UsesColumnsForWidthAndRowsForHeight(int rows, int columns, int width, int height)
    {
        Assert.True(PoiLayoutGeometry.TryGetAutoFitSize(RectangleCorners, rows, columns, GraphicTypes.Rect, out Size size));
        Assert.Equal(new Size(width, height), size);
        Assert.True(PoiLayoutGeometry.TryOffsetForRectangle(RectangleCorners, size.Width, size.Height, DrawingGraphicPosition.Internal, out List<Point> inset));
        Assert.Equal(rows * columns, PoiLayoutGeometry.CreateQuadrilateralGrid(inset, rows, columns).Count);
    }

    [Theory]
    [InlineData(2, 5, 10)]
    [InlineData(3, 3, 8)]
    [InlineData(1, 1, 24)]
    [InlineData(1, 3, 16)]
    [InlineData(3, 1, 8)]
    public void AutoFitCircle_ReturnsEvenDiameterForIntegerRadius(int rows, int columns, int radius)
    {
        Assert.True(PoiLayoutGeometry.TryGetAutoFitSize(RectangleCorners, rows, columns, GraphicTypes.Circle, out Size size));
        Assert.Equal(new Size(radius * 2, radius * 2), size);
        Assert.True(PoiLayoutGeometry.TryOffsetForCircle(RectangleCorners, radius, DrawingGraphicPosition.Internal, out _));
    }

    [Theory]
    [InlineData(GraphicTypes.Rect, 1, 1)]
    [InlineData(GraphicTypes.Rect, 2, 5)]
    [InlineData(GraphicTypes.Circle, 1, 1)]
    [InlineData(GraphicTypes.Circle, 2, 5)]
    public void AutoFitSkewedArea_AllGeneratedWindowsStayInside(GraphicTypes shape, int rows, int columns)
    {
        Point[] corners = [new(10, 10), new(110, 20), new(90, 100), new(0, 80)];
        Assert.True(PoiLayoutGeometry.TryGetAutoFitSize(corners, rows, columns, shape, out Size size));
        Assert.True(size.Width >= 1 && size.Height >= 1);
        List<Point> inset;
        Assert.True(shape == GraphicTypes.Circle
            ? PoiLayoutGeometry.TryOffsetForCircle(corners, size.Width / 2, DrawingGraphicPosition.Internal, out inset)
            : PoiLayoutGeometry.TryOffsetForRectangle(corners, size.Width, size.Height, DrawingGraphicPosition.Internal, out inset));

        foreach (Point center in PoiLayoutGeometry.CreateQuadrilateralGrid(inset, rows, columns))
        {
            for (int edgeIndex = 0; edgeIndex < corners.Length; edgeIndex++)
            {
                Point start = corners[edgeIndex];
                Vector edge = corners[(edgeIndex + 1) % corners.Length] - start;
                Vector normal = new(-edge.Y, edge.X);
                normal.Normalize();
                double support = shape == GraphicTypes.Circle ? size.Width / 2
                    : Math.Abs(normal.X) * size.Width / 2 + Math.Abs(normal.Y) * size.Height / 2;
                Assert.True(Vector.Multiply(center - start, normal) >= support - 1e-8);
            }
        }
    }

    [Fact]
    public void AutoFit_NormalizesCornerOrder()
    {
        Assert.True(PoiLayoutGeometry.TryGetAutoFitSize(RectangleCorners.Reverse().ToArray(), 2, 5, GraphicTypes.Rect, out Size size));
        Assert.Equal(new Size(20, 25), size);
    }

    [Theory]
    [InlineData(0, 3, GraphicTypes.Rect)]
    [InlineData(3, -1, GraphicTypes.Circle)]
    [InlineData(3, 3, GraphicTypes.Polygon)]
    [InlineData(int.MaxValue, int.MaxValue, GraphicTypes.Rect)]
    public void AutoFit_RejectsInvalidOrSubpixelSamplingSizes(int rows, int columns, GraphicTypes shape)
    {
        Assert.False(PoiLayoutGeometry.TryGetAutoFitSize(RectangleCorners, rows, columns, shape, out _));
    }

    [Fact]
    public void AutoFit_RejectsInvalidAreasAndKeepsManualCollapsedPointGeometryAvailable()
    {
        Point[] collapsed = [new(50, 25), new(50, 25), new(50, 25), new(50, 25)];
        Point[] nonfinite = [new(double.NaN, 0), new(100, 0), new(100, 50), new(0, 50)];
        Assert.False(PoiLayoutGeometry.TryGetAutoFitSize(collapsed, 1, 1, GraphicTypes.Rect, out _));
        Assert.False(PoiLayoutGeometry.TryGetAutoFitSize(nonfinite, 2, 3, GraphicTypes.Circle, out _));
        Assert.True(PoiLayoutGeometry.TryGetCollapsedPoint(collapsed, out Point point));
        Assert.Equal(new Point(50, 25), point);
    }

    private static void AssertPointsEqual(IReadOnlyList<Point> expected, IReadOnlyList<Point> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].X, actual[index].X, precision: 8);
            Assert.Equal(expected[index].Y, actual[index].Y, precision: 8);
        }
    }
}
