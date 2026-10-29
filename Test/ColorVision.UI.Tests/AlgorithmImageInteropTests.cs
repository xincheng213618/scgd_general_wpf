using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;

namespace ColorVision.UI.Tests;

public class AlgorithmImageInteropTests
{
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
