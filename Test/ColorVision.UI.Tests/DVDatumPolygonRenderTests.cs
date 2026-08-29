using ColorVision.ImageEditor.Draw;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class DVDatumPolygonRenderTests
{
    [Fact]
    public void ClosedPolygonUsesConfiguredPenForEveryEdge()
    {
        WpfTestHost.Invoke(() =>
        {
            Point[] points =
            [
                new Point(10, 10),
                new Point(50, 10),
                new Point(50, 40),
                new Point(10, 40)
            ];
            DVDatumPolygon polygon = new() { IsComple = true };
            polygon.Pen = new Pen(Brushes.Blue, 1);
            polygon.Points.AddRange(points);

            polygon.Render();

            List<GeometryDrawing> edges = EnumerateDrawings(VisualTreeHelper.GetDrawing(polygon))
                .OfType<GeometryDrawing>()
                .Where(drawing => drawing.Geometry is LineGeometry)
                .ToList();

            Assert.Equal(4, edges.Count);
            Assert.All(edges, edge =>
            {
                SolidColorBrush brush = Assert.IsType<SolidColorBrush>(edge.Pen?.Brush);
                Assert.Equal(Colors.Blue, brush.Color);
                Assert.Equal(1, edge.Pen?.Thickness);
            });
            Assert.Contains(edges, edge => edge.Geometry is LineGeometry line &&
                line.StartPoint == points[^1] && line.EndPoint == points[0]);
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
