using ColorVision.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmAnalysisImagePresentationTests
{
    [Fact]
    public async Task PaddedBgr48PreviewRetainsTheOriginalBitDepthAfterTheSourceIsDisposed()
    {
        byte[] pixels =
        [
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 90,
            13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 91,
        ];
        using AlgorithmImageArtifact image = new("result", "primary",
            new AlgorithmImageBuffer(2, 2, 13, AlgorithmImageFormat.Bgr48, pixels, 135, 119));
        using AlgorithmAnalysisImagePresentation presentation = new(image, retainPixels: true);
        image.Dispose();

        BitmapSource preview = Assert.IsAssignableFrom<BitmapSource>(await presentation.GetPreviewAsync(default));

        Assert.True(preview.IsFrozen);
        Assert.Equal(PixelFormats.Rgb48, preview.Format);
        Assert.Equal((2, 2, 135d, 119d), (preview.PixelWidth, preview.PixelHeight, preview.DpiX, preview.DpiY));
        byte[] actual = new byte[24];
        preview.CopyPixels(actual, 12, 0);
        Assert.Equal(new byte[] { 5, 6, 3, 4, 1, 2, 11, 12, 9, 10, 7, 8, 17, 18, 15, 16, 13, 14, 23, 24, 21, 22, 19, 20 }, actual);
    }
}
