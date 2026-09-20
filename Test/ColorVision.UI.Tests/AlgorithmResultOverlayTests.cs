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
    public void FovLabelCirclesFollowTheResultOverlayFontSize()
    {
        WpfTestHost.Invoke(() =>
        {
            var context = CreateFovContext();
            context.Canvas.IsLayoutUpdated = false;
            context.Canvas.TextFontSizeOverride = 80;
            FovMeasurement measurement = new()
            {
                Corners =
                [
                    new LuminousAreaPoint(10, 20),
                    new LuminousAreaPoint(210, 20),
                    new LuminousAreaPoint(210, 120),
                    new LuminousAreaPoint(10, 120)
                ]
            };

            FovOverlayRenderer.Render(
                context.ImageContext,
                context.DrawContext,
                measurement);

            DVCircleText[] labels = context.Canvas.Visuals.OfType<DVCircleText>().ToArray();
            Assert.All(labels, label =>
            {
                Assert.Equal(80, label.TextAttribute.FontSize);
                Assert.Equal(40, label.Radius);
            });

            context.Canvas.TextFontSizeOverride = 40;
            context.Canvas.ApplyLayoutScaleToVisuals();

            Assert.All(labels, label =>
            {
                Assert.Equal(40, label.TextAttribute.FontSize);
                Assert.Equal(20, label.Radius);
            });
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
