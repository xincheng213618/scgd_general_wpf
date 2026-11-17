using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;

namespace ColorVision.UI.Tests;

public class AlgorithmImageInteropTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FromMatCopiesOnlyPixelsIntoAnIndependentTightBuffer(bool crop)
    {
        const int width = 513;
        const int height = 513;
        const int bytesPerPixel = 4;
        int left = crop ? 2 : 0;
        int storageWidth = crop ? width + 5 : width;
        int stride = width * bytesPerPixel;
        int storageStride = storageWidth * bytesPerPixel;
        byte[] pixels = new byte[height * storageStride];
        new Random(4337).NextBytes(pixels);
        byte[] expected = new byte[height * stride];
        for (int y = 0; y < height; y++)
            pixels.AsSpan(y * storageStride + left * bytesPerPixel, stride).CopyTo(expected.AsSpan(y * stride, stride));
        using Mat owner = Mat.FromPixelData(height, storageWidth, MatType.CV_8UC4, pixels);
        using Mat source = new(owner, new Rect(left, 0, width, height));

        using AlgorithmImageBuffer output = AlgorithmImageInterop.FromMat(source, dpiX: 137, dpiY: 119);
        Assert.Equal(!crop, source.IsContinuous());
        Assert.Equal((width, height, stride, AlgorithmImageFormat.Bgra32, 137d, 119d),
            (output.Width, output.Height, output.Stride, output.Format, output.DpiX, output.DpiY));
        owner.SetTo(Scalar.Black);

        Assert.True(expected.AsSpan().SequenceEqual(output.Data.Span));
    }

    // This verifies rejection at entry with an already-cancelled token.
    // In-flight native stages and row copying require separate trace evidence.
    [Fact]
    public void FromMatRejectsPreCancelledTokenWithoutReturningBufferOrChangingSource()
    {
        using Mat owner = Mat.FromArray(new byte[,] { { 11, 12, 13, 14 }, { 21, 22, 23, 24 }, { 31, 32, 33, 34 } });
        using Mat originalOwner = owner.Clone();
        using Mat source = new(owner, new Rect(1, 1, 2, 2));
        Assert.False(source.IsContinuous());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        AlgorithmImageBuffer? output = null;
        try
        {
            OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
                output = AlgorithmImageInterop.FromMat(source, 144, 192, cancellation.Token));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.Null(output);
            Assert.Equal((byte)22, source.At<byte>(0, 0));
            Assert.Equal(0d, Cv2.Norm(originalOwner, owner, NormTypes.INF));
        }
        finally
        {
            output?.Dispose();
        }
    }
}
