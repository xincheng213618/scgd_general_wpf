using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class ImageDrawingPresentationTests
{
    [Fact]
    public void AttachFollowsComponentInitializationAndDisposeDetachesConfigurationAndVisualEvents()
    {
        WpfTestHost.Invoke(() =>
        {
            using DrawCanvas canvas = new();
            DrawEditorContext draw = new(canvas, new Zoombox());
            ImageViewConfig config = new() { IsShowText = false, IsShowMsg = false, IsLayoutUpdated = false, DrawingTextFontSize = 10 };
            using ImageDrawingPresentation presentation = new(draw, config);
            try
            {
                DVRectangleText componentVisual = CreateRectangle("component");
                canvas.AddVisual(componentVisual);
                Assert.Empty(draw.DrawingVisualLists);

                presentation.Attach();
                presentation.Attach();
                DVRectangleText addedVisual = CreateRectangle("added");
                presentation.AddVisual(addedVisual);
                Assert.Same(addedVisual, Assert.Single(draw.DrawingVisualLists));
                Assert.False(addedVisual.Attribute.IsShowText);
                Assert.False(addedVisual.IsMessageVisible);
                Assert.NotNull(addedVisual.Drawing);

                config.IsShowText = true;
                config.IsShowMsg = true;
                config.DrawingTextFontSize = 12;
                Assert.True(addedVisual.Attribute.IsShowText);
                Assert.True(addedVisual.IsMessageVisible);
                Assert.Equal(12, canvas.TextFontSizeOverride);

                presentation.Dispose();
                config.IsShowText = false;
                config.IsShowMsg = false;
                config.IsLayoutUpdated = true;
                config.DrawingTextFontSize = 24;
                canvas.AddVisual(CreateRectangle("after-dispose"));

                Assert.Same(addedVisual, Assert.Single(draw.DrawingVisualLists));
                Assert.True(addedVisual.Attribute.IsShowText);
                Assert.True(addedVisual.IsMessageVisible);
                Assert.False(canvas.IsLayoutUpdated);
                Assert.Equal(12, canvas.TextFontSizeOverride);
            }
            finally { draw.MouseInfoProvider.Dispose(); }
        });
    }

    private static DVRectangleText CreateRectangle(string text) => new(new RectangleTextProperties
    {
        Rect = new Rect(0, 0, 30, 20),
        Pen = new Pen(Brushes.Red, 1),
        Text = text,
        Msg = "detail",
    });
}
