using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class PoiCanvasTests
{
    [Fact]
    public void CreateSolidColorDrawingUsesFrozenVectorWithRequestedLogicalSize()
    {
        DrawingImage image = ImageUtils.CreateSolidColorDrawing(9680, 5460, Colors.White);

        Assert.True(image.IsFrozen);
        Assert.Equal(9680, image.Width);
        Assert.Equal(5460, image.Height);
        Assert.IsNotAssignableFrom<BitmapSource>(image);
    }

    [Fact]
    public void TryGetImageSizeUsesLogicalSizeForVectorCanvas()
    {
        DrawingImage image = ImageUtils.CreateSolidColorDrawing(9680, 5460, Colors.White);

        bool found = ImageUtils.TryGetImageSize(image, out int width, out int height);

        Assert.True(found);
        Assert.Equal(9680, width);
        Assert.Equal(5460, height);
    }

    [Fact]
    public void TryGetImageSizePrefersBitmapPixelDimensionsForRealImage()
    {
        WriteableBitmap image = new(32, 24, 192, 192, PixelFormats.Gray8, null);

        bool found = ImageUtils.TryGetImageSize(image, out int width, out int height);

        Assert.True(found);
        Assert.Equal(32, width);
        Assert.Equal(24, height);
    }

    [Fact]
    public void AddVisualsAddsUniqueVisualsWithOneBatchEventAndNoUndoHistory()
    {
        StaTest.Run(() =>
        {
            using DrawCanvas canvas = new();
            DrawingVisual first = new();
            DrawingVisual second = new();
            int eventCount = 0;
            VisualChangedEventArgs? eventArgs = null;
            canvas.VisualsAdd += (_, args) =>
            {
                eventCount++;
                eventArgs = args;
            };

            int added = canvas.AddVisuals(new Visual[] { first, second, first });

            Assert.Equal(2, added);
            Assert.Equal(2, canvas.Visuals.Count);
            Assert.Equal(1, eventCount);
            Assert.NotNull(eventArgs);
            Assert.Equal(VisualChangeType.AddRange, eventArgs.ChangeType);
            Assert.Equal(2, eventArgs.Visuals.Count);
            Assert.Empty(canvas.UndoStack);
        });
    }
}
