using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System.Buffers.Binary;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class RgbCrossPoolingTests
{
    [Theory]
    [InlineData(37, 31, 0.1)]
    [InlineData(1603, 31, 1.0)]
    [InlineData(3203, 31, 2.2)]
    [InlineData(8003, 31, 5.0)]
    [InlineData(3200, 32, 1.0)]
    [InlineData(37, 3203, 2.2)]
    public void PoolingPreservesEveryChannelWithPartialCellsAndUnalignedStride(int searchWidth, int searchHeight, double exponent)
    {
        int width = searchWidth + 11, height = searchHeight + 13;
        AlgorithmImageBuffer Buffer(AlgorithmImageFormat format)
        {
            int count = format.Channels(), stride = width * count * 2 + 7;
            var data = new byte[stride * height];
            Array.Fill(data, (byte)255); // Bright padding must stay outside the ROI.
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    for (int c = 0; c < count; c++)
                    {
                        ushort value = c == 3 ? ushort.MaxValue : unchecked((ushort)(x * 73856093 ^ y * 19349663 ^ c * 83492791));
                        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(y * stride + (x * count + c) * 2, 2), value);
                    }
            return new AlgorithmImageBuffer(width, height, stride, format, data);
        }
        using var bgr = Buffer(AlgorithmImageFormat.Bgr48);
        using var bgra = Buffer(AlgorithmImageFormat.Bgra64);
        var pool = typeof(DisplayMetrologyProvider).GetMethod("CrossLocatorImage", BindingFlags.Static | BindingFlags.NonPublic)!;
        (float[][] Channels, int Width, int Height, int Step) Pool(AlgorithmImageBuffer image)
            => ((float[][], int, int, int))pool.Invoke(null, [image, new Rect(3, 5, searchWidth, searchHeight), exponent, CancellationToken.None])!;
        var actual = Pool(bgr);
        var expected = Pool(bgra); // Opaque BGRA retains the independent generic path.
        Assert.Equal((expected.Width, expected.Height, expected.Step), (actual.Width, actual.Height, actual.Step));
        for (int c = 0; c < 3; c++)
            Assert.True(actual.Channels[c].AsSpan().SequenceEqual(expected.Channels[c]), $"Channel {c} differs from the generic reader.");
    }
}
