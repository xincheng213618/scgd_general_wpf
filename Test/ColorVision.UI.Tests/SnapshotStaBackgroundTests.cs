using ColorVision.ImageEditor.Output;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class SnapshotStaBackgroundTests
{
    [Fact]
    public async Task ReusedBackgroundDoesNotChangePreviouslyRenderedPixels()
    {
        (RenderTargetBitmap first, RenderTargetBitmap second) = await SnapshotStaWorker.RunAsync(() =>
        {
            try
            {
                WriteableBitmap scratch = Assert.IsType<WriteableBitmap>(SnapshotStaWorker.TakeOpaqueBackground(1, 1, 96, 96));
                RenderTargetBitmap first = RenderPixel(scratch, [3, 17, 29, 255]);
                SnapshotStaWorker.ReturnOpaqueBackground(scratch);
                Assert.Same(scratch, SnapshotStaWorker.TakeOpaqueBackground(1, 1, 96, 96));
                RenderTargetBitmap second = RenderPixel(scratch, [65, 37, 9, 255]);

                SnapshotStaWorker.ReturnOpaqueBackground(scratch);
                Assert.Null(SnapshotStaWorker.TakeOpaqueBackground(8193, 8192, 96, 96));
                Assert.NotSame(scratch, SnapshotStaWorker.TakeOpaqueBackground(1, 1, 96, 96));

                SnapshotStaWorker.ReturnOpaqueBackground(scratch);
                Assert.NotSame(scratch, SnapshotStaWorker.TakeOpaqueBackground(1, 1, 120, 96));

                return (first, second);
            }
            finally
            {
                SnapshotStaWorker.ReleaseOpaqueBackground();
            }
        }, default);

        byte[] pixel = new byte[4];
        first.CopyPixels(pixel, 4, 0);
        Assert.Equal(new byte[] { 3, 17, 29, 255 }, pixel);
        second.CopyPixels(pixel, 4, 0);
        Assert.Equal(new byte[] { 65, 37, 9, 255 }, pixel);
    }

    private static RenderTargetBitmap RenderPixel(WriteableBitmap scratch, byte[] pixel)
    {
        scratch.WritePixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
        ImageDrawing image = new(scratch, new Rect(0, 0, 1, 1));
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
            context.DrawDrawing(image);

        RenderTargetBitmap bitmap = new(1, 1, 96, 96, PixelFormats.Pbgra32);
        try
        {
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            image.ImageSource = null;
        }
    }
}
