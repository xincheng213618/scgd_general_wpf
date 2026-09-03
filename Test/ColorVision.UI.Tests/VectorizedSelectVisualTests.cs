using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates.Ghost;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class VectorizedSelectVisualTests
{
    [Fact]
    public void GhostPixelsUseImageBoundsAndFrozenVectorGeometry()
    {
        StaTest.Run(() =>
        {
            List<Point1> points = new()
            {
                new Point1 { X = 12, Y = 20 },
                new Point1 { X = 9000, Y = 5000 }
            };
            DrawingImage source = ImageUtils.CreateSolidColorDrawing(9680, 5460, Colors.White);

            VectorizedSelectVisual visual = Assert.IsType<VectorizedSelectVisual>(ViewHandleGhost.CreatePixelVisual(points, source));

            Assert.Equal(new Rect(0, 0, 9680, 5460), visual.GetRect());
            Assert.Same(points, visual.Attribute.Tag);
            GeometryDrawing drawing = Assert.IsType<GeometryDrawing>(visual.VectorDrawing);
            Assert.True(drawing.IsFrozen);
            Assert.IsType<StreamGeometry>(drawing.Geometry);
            Assert.DoesNotContain(EnumerateDrawings(VisualTreeHelper.GetDrawing(visual)), item => item is ImageDrawing);

            Rect contentBounds = VisualTreeHelper.GetContentBounds(visual);
            Assert.True(contentBounds.Right > 9000);
            Assert.True(contentBounds.Bottom > 5000);
        });
    }

    [Fact]
    public void GhostPixelsFallBackToPointBoundsAndSkipEmptySets()
    {
        StaTest.Run(() =>
        {
            List<Point1> points = new()
            {
                new Point1 { X = 10, Y = 30 },
                new Point1 { X = 40, Y = 70 }
            };

            VectorizedSelectVisual visual = Assert.IsType<VectorizedSelectVisual>(ViewHandleGhost.CreatePixelVisual(points, null));

            Assert.Equal(new Rect(10, 30, 31, 41), visual.GetRect());
            Assert.Null(ViewHandleGhost.CreatePixelVisual(Array.Empty<Point1>(), null));
        });
    }

    [Fact]
    public void BuildPoiUsesOneFrozenVectorForTheWholePointGrid()
    {
        StaTest.Run(() =>
        {
            List<POIPointPosition> positions = new()
            {
                new POIPointPosition { PixelX = 12, PixelY = 20 },
                new POIPointPosition { PixelX = 9000, PixelY = 5000 },
            };
            POIHeaderInfo header = new() { Width = 8, Height = 6, Rows = 100, Cols = 100 };
            DrawingImage source = ImageUtils.CreateSolidColorDrawing(9680, 5460, Colors.White);

            VectorizedSelectVisual visual = Assert.IsType<VectorizedSelectVisual>(
                ViewHandleBuildPoiFile.CreatePoiVisual(positions, header, source));

            Assert.Equal(new Rect(0, 0, 9680, 5460), visual.GetRect());
            Assert.Same(positions, visual.Attribute.Tag);
            GeometryDrawing drawing = Assert.IsType<GeometryDrawing>(visual.VectorDrawing);
            Assert.True(drawing.IsFrozen);
            Assert.IsType<StreamGeometry>(drawing.Geometry);
            Assert.DoesNotContain(EnumerateDrawings(VisualTreeHelper.GetDrawing(visual)), item => item is ImageDrawing);

            Rect contentBounds = VisualTreeHelper.GetContentBounds(visual);
            Assert.True(contentBounds.Right > 9000);
            Assert.True(contentBounds.Bottom > 5000);
        });
    }

    private static IEnumerable<Drawing> EnumerateDrawings(Drawing? drawing)
    {
        if (drawing == null)
            yield break;

        yield return drawing;
        if (drawing is DrawingGroup group)
        {
            foreach (Drawing child in group.Children)
            {
                foreach (Drawing descendant in EnumerateDrawings(child))
                    yield return descendant;
            }
        }
    }
}
