using ColorVision.Engine.Media;
using OpenCvSharp;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class MediaHelperTests
{
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
