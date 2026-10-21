using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

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

    [Fact]
    public void FovRendererCreatesSharedGeometryAndLabels()
    {
        WpfTestHost.Invoke(() =>
        {
            var context = CreateFovContext();
            FovMeasurement measurement = new()
            {
                Corners =
                [
                    new LuminousAreaPoint(10, 20),
                    new LuminousAreaPoint(210, 20),
                    new LuminousAreaPoint(210, 120),
                    new LuminousAreaPoint(10, 120)
                ],
                DirectionalHorizontalFovDegrees = 24.2096,
                DirectionalVerticalFovDegrees = 9.8688,
                DiagonalFovDegrees = 26.0354
            };

            FovOverlayRenderer.Render(
                context.ImageContext,
                context.DrawContext,
                measurement);

            Assert.Single(context.Canvas.Visuals.OfType<DVPolygon>());
            Assert.Equal(4, context.Canvas.Visuals.OfType<DVLine>().Count());
            DVCircleText[] labels = context.Canvas.Visuals.OfType<DVCircleText>().ToArray();
            Assert.Equal(7, labels.Length);
            Assert.Contains(labels, label => label.Attribute.Msg == "H 24.2096°");
            Assert.Contains(labels, label => label.Attribute.Msg == "V 9.8688°");
            Assert.Contains(labels, label => label.Attribute.Msg == "D 26.0354°");
            Assert.All(context.Canvas.Visuals.OfType<DrawingVisualBase>(), visual =>
                Assert.Equal(AlgorithmResultOverlay.FovTag, visual.BaseAttribute.Tag));
        });
    }

    [Fact]
    public void CurvedFovRendererUsesMeasuredContourAndCenterEndpoints()
    {
        WpfTestHost.Invoke(() =>
        {
            var context = CreateFovContext();
            FovMeasurement measurement = FovCalculator.Calculate(
                [new(10, 20), new(210, 20), new(210, 120), new(10, 120)], 9410, 74.2) with
            {
                Mode = FovMeasurementMode.CurvedBoundary,
                Corners = Array.Empty<LuminousAreaPoint>(),
                Boundary = new FovBoundaryGeometry
                {
                    Center = new(110, 70), Left = new(35, 70), Right = new(185, 70), Top = new(110, 35), Bottom = new(110, 105),
                    DiagonalPoints = [new(10, 20), new(210, 20), new(210, 120), new(10, 120)],
                    Contour = [new(10, 20), new(110, 35), new(210, 20), new(185, 70), new(210, 120), new(110, 105), new(10, 120), new(35, 70)]
                }
            };
            FovOverlayRenderer.Render(context.ImageContext, context.DrawContext, measurement);
            DVPolygon contour = Assert.Single(context.Canvas.Visuals.OfType<DVPolygon>());
            Assert.Equal(measurement.Boundary.Contour.Select(point => new Point(point.X, point.Y)), contour.Attribute.Points);
            DVLine horizontal = Assert.Single(context.Canvas.Visuals.OfType<DVLine>(), line => line.Attribute.Pen.Brush == Brushes.Orange);
            Assert.Equal(new[] { new Point(35, 70), new Point(185, 70) }, horizontal.Attribute.Points);
            DVLine vertical = Assert.Single(context.Canvas.Visuals.OfType<DVLine>(), line => line.Attribute.Pen.Brush == Brushes.LimeGreen);
            Assert.Equal(new[] { new Point(110, 35), new Point(110, 105) }, vertical.Attribute.Points);
            Assert.Equal(3, context.Canvas.Visuals.OfType<DVCircleText>().Count());
        });
    }

    [Fact]
    public void GridRendererUsesMeasuredCoordinatesAndRejectsIncompleteOrUnorderedPoints()
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new();
            DrawEditorContext context = new(canvas, new Zoombox());
            Point[] points =
            [
                new(10, 20), new(60, 15), new(110, 20),
                new(5, 70), new(60, 70), new(115, 70),
                new(10, 120), new(60, 125), new(110, 120)
            ];
            GridDistortionOverlayRenderer.AppendLines(context, points, 3, 3, AlgorithmResultOverlay.GridDistortionTag);
            Assert.Equal(16, canvas.Visuals.OfType<DVLine>().Count());
            Assert.Contains(canvas.Visuals.OfType<DVLine>(), line => line.Points.SequenceEqual([points[0], points[1]]));
            AlgorithmResultOverlay.ClearTagged(context, AlgorithmResultOverlay.GridDistortionTag);

            GridDistortionOverlayRenderer.AppendLines(context, points.Take(8).ToArray(), 3, 3, AlgorithmResultOverlay.GridDistortionTag);
            (points[0], points[1]) = (points[1], points[0]);
            GridDistortionOverlayRenderer.AppendLines(context, points, 3, 3, AlgorithmResultOverlay.GridDistortionTag);
            Assert.Empty(canvas.Visuals.OfType<DVLine>());
        });
    }

    private static (ImageProcessingContext ImageContext, DrawEditorContext DrawContext, DrawCanvas Canvas) CreateFovContext()
    {
        DrawCanvas canvas = new();
        Zoombox zoombox = new()
        {
            Child = canvas,
            ContentMatrix = Matrix.Identity
        };
        DrawEditorContext drawContext = new(canvas, zoombox);
        Guid documentId = Guid.NewGuid();
        ImageProcessingContext imageContext = new(
            new ImageViewConfig(),
            canvas,
            Dispatcher.CurrentDispatcher,
            new ImageProcessingContextBinding
            {
                IsInitialized = () => false,
                GetDocumentInstanceId = () => documentId,
                IsDisposed = () => false,
                GetImageRevision = () => 0,
                AcquireImageFrame = () => null,
                IsCurrentImageRevision = _ => true,
                NotifySourcePixelsChanged = () => { },
                CommitSourcePixels = _ => { },
                GetViewBitmapSource = () => null,
                SetViewBitmapSource = _ => { },
                GetSelectedLayerSourceChannelIndex = () => -1,
                SetImageSource = _ => { },
                UpdateZoomAndScale = () => { }
            });
        drawContext.ProcessingContext = imageContext;
        return (imageContext, drawContext, canvas);
    }
}
