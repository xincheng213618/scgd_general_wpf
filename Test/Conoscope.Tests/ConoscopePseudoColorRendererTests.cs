using Conoscope.Core;
using OpenCvSharp;

namespace Conoscope.Tests;

public class ConoscopePseudoColorRendererTests
{
    [Fact]
    public void PercentileIsExactForSmallMaskedInputAndIgnoresNonFiniteValues()
    {
        using Mat values = Mat.FromArray(new float[,]
        {
            { 50, float.NaN, 10, 40, float.PositiveInfinity, 20, 30 }
        });
        using Mat mask = Mat.FromArray(new byte[,]
        {
            { 255, 255, 0, 255, 255, 255, 255 }
        });

        double median = ConoscopePseudoColorRenderer.GetMaskedPercentile(values, mask, 0.5);

        Assert.Equal(40, median);
    }

    [Fact]
    public void PercentileReturnsNaNWhenMaskContainsNoFinitePixels()
    {
        using Mat values = Mat.FromArray(new float[,] { { 1, 2, 3 } });
        using Mat mask = Mat.Zeros(1, 3, MatType.CV_8UC1);

        double result = ConoscopePseudoColorRenderer.GetMaskedPercentile(values, mask, 0.995);

        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void HeightMapMaskPreservesFractionalCircleBoundary()
    {
        const int width = 4;
        const int height = 4;
        System.Windows.Point center = new(0.5, 0.5);
        const double radius = 1.5;
        using Mat x = new(height, width, MatType.CV_32FC1, Scalar.All(1));
        using Mat y = new(height, width, MatType.CV_32FC1, Scalar.All(2));
        using Mat z = new(height, width, MatType.CV_32FC1, Scalar.All(3));

        var bitmap = ConoscopePseudoColorRenderer.CreateHeightMapBitmap(
            x,
            y,
            z,
            ExportChannel.Y,
            () => throw new InvalidOperationException(),
            () => throw new InvalidOperationException(),
            center,
            radius);

        byte[] pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        double radiusSquared = radius * radius;
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                double deltaX = column - center.X;
                double deltaY = row - center.Y;
                byte expectedAlpha = deltaX * deltaX + deltaY * deltaY <= radiusSquared ? (byte)255 : (byte)0;
                Assert.Equal(expectedAlpha, pixels[(row * width + column) * 4 + 3]);
            }
        }
    }

    [Fact]
    public void DerivedChannelFallbackDoesNotDisposeSourceMat()
    {
        using Mat x = new(2, 2, MatType.CV_32FC1, Scalar.All(1));
        using Mat y = new(2, 2, MatType.CV_32FC1, Scalar.All(2));
        using Mat z = new(2, 2, MatType.CV_32FC1, Scalar.All(3));

        _ = ConoscopePseudoColorRenderer.Render(
            x,
            y,
            z,
            ExportChannel.Contrast,
            ColorVision.Core.ColormapTypes.COLORMAP_JET,
            () => y,
            () => y,
            usePseudoColor: false);

        Assert.Equal(2, y.At<float>(0, 0));
    }
}
