using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmResultOverlayTests
{
    [Fact]
    public void AddPolygonPreservesRequestedStrokeBrush()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            DrawEditorContext context = new(canvas, new Zoombox());

            AlgorithmResultOverlay.AddPolygon(
                context,
                [new Point(10, 10), new Point(100, 10), new Point(100, 80), new Point(10, 80)],
                new Pen(Brushes.DeepSkyBlue, 2),
                AlgorithmResultOverlay.FindLuminousAreaTag);

            DVPolygon polygon = Assert.Single(canvas.Visuals.OfType<DVPolygon>());
            Assert.Equal(Brushes.DeepSkyBlue, polygon.Attribute.Pen.Brush);
        });
    }
}
