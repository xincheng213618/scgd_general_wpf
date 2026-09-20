using ColorVision.Engine.Media;
using OpenCvSharp;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class MediaHelperTests
{
    [Fact]
    public void MatUpdateWriteableBitmapRejectsMatchingFrozenTargetWithoutChangingPixels()
    {
        WpfTestHost.Invoke(() =>
        {
            using Mat source = new(2, 2, MatType.CV_8UC1, Scalar.All(200));
            WriteableBitmap bitmap = new(2, 2, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, 2, 2), new byte[] { 10, 11, 12, 13 }, 2, 0);
            bitmap.Freeze();

            Assert.False(source.MatUpdateWriteableBitmap(bitmap));

            byte[] pixels = new byte[4];
            bitmap.CopyPixels(pixels, 2, 0);
            Assert.Equal(new byte[] { 10, 11, 12, 13 }, pixels);
        });
    }

    [Fact]
    public void MatUpdateWriteableBitmapRejectsSameByteCountWithDifferentFormats()
    {
        using Mat floatGray = new(2, 2, MatType.CV_32FC1, Scalar.All(1));
        using Mat byteBgra = new(2, 2, MatType.CV_8UC4, Scalar.All(1));
        WriteableBitmap bgraBitmap = new(2, 2, 96, 96, PixelFormats.Bgra32, null);
        WriteableBitmap floatBitmap = new(2, 2, 96, 96, PixelFormats.Gray32Float, null);

        Assert.False(floatGray.MatUpdateWriteableBitmap(bgraBitmap));
        Assert.False(byteBgra.MatUpdateWriteableBitmap(floatBitmap));
    }

    [Fact]
    public void MatUpdateWriteableBitmapReusesExactFormatAndSize()
    {
        using Mat source = new(2, 2, MatType.CV_8UC4, new Scalar(1, 2, 3, 4));
        WriteableBitmap bitmap = new(2, 2, 96, 96, PixelFormats.Bgra32, null);

        Assert.True(source.MatUpdateWriteableBitmap(bitmap));
    }
}
