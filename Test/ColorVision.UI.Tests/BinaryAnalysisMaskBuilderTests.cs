using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class BinaryAnalysisMaskBuilderTests
{
    [Theory]
    [InlineData(128, true)]
    [InlineData(128.5, true)]
    [InlineData(128.5, false)]
    [InlineData(0, false)]
    [InlineData(255, true)]
    public void Gray8RectangleKeepsInclusiveThresholdAndRespectsStride(double threshold, bool bright)
    {
        const int width = 71, height = 4, stride = 77;
        const int left = 3, top = 1, roiWidth = 65, roiHeight = 2;
        byte[] samples = [0, 127, 128, 129, 254, 255];
        byte[] pixels = new byte[stride * height];
        for (int index = 0; index < pixels.Length; index++) pixels[index] = samples[index % samples.Length];
        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Gray8, pixels);
        AlgorithmPixelRoi roi = AlgorithmPixelRoi.Create(new RectangleAlgorithmRoi(left, top, roiWidth, roiHeight), image);
        byte[] mask = new byte[width * height];
        Array.Fill(mask, (byte)17);
        byte[] expected = (byte[])mask.Clone();
        int foreground = 0;
        for (int y = top; y < top + roiHeight; y++)
        for (int x = left; x < left + roiWidth; x++)
        {
            byte pixel = pixels[y * stride + x];
            if (!(bright ? pixel >= threshold : pixel <= threshold)) continue;
            expected[y * width + x] = byte.MaxValue;
            foreground++;
        }

        BinaryAnalysisMaskSummary result = BinaryAnalysisMaskBuilder.Build(image, roi, threshold, bright, mask, default);

        Assert.Equal(new BinaryAnalysisMaskSummary(roiWidth * roiHeight, foreground, 0), result);
        Assert.Equal(expected, mask);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Gray16RectanglePreservesRoundedCutsInPaddedRows(bool bright)
    {
        const int width = 73, height = 4, stride = width * sizeof(ushort) + 3;
        const int left = 3, top = 1, roiWidth = 65, roiHeight = 2;
        const ushort boundary = 32123;
        const double scale = 255.0 / ushort.MaxValue;
        const double nominalBoundary = boundary * scale;
        ushort[] samples = [0, boundary - 1, boundary, boundary + 1, ushort.MaxValue];
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(y * stride + x * sizeof(ushort)),
                    samples[(x + y) % samples.Length]);
        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Gray16, pixels);
        AlgorithmPixelRoi roi = AlgorithmPixelRoi.Create(new RectangleAlgorithmRoi(left, top, roiWidth, roiHeight), image);

        foreach (double threshold in new[] { 0, Math.BitDecrement(nominalBoundary), nominalBoundary, Math.BitIncrement(nominalBoundary), ushort.MaxValue * scale })
        {
            byte[] mask = new byte[width * height];
            Array.Fill(mask, (byte)17);
            byte[] expected = (byte[])mask.Clone();
            int foreground = 0;
            for (int y = top; y < top + roiHeight; y++)
                for (int x = left; x < left + roiWidth; x++)
                {
                    double intensity = samples[(x + y) % samples.Length] * scale;
                    if (!(bright ? intensity >= threshold : intensity <= threshold)) continue;
                    expected[y * width + x] = byte.MaxValue;
                    foreground++;
                }

            BinaryAnalysisMaskSummary result = BinaryAnalysisMaskBuilder.Build(image, roi, threshold, bright, mask, default);

            Assert.Equal(new BinaryAnalysisMaskSummary(roiWidth * roiHeight, foreground, 0), result);
            Assert.Equal(expected, mask);
        }
    }

    [Theory]
    [InlineData(0, 6, 4)]
    [InlineData(0.5, 2, 2)]
    public void RectangleIncludesOnlyItsPixelCentersWithinPaddedRows(double origin, int included, int foreground)
    {
        ushort[] rows = [0, 32896, 65535, 65535, 0, 65535, 32896, 65535];
        using AlgorithmImageBuffer image = new(3, 2, 8, AlgorithmImageFormat.Gray16, MemoryMarshal.AsBytes(rows.AsSpan()).ToArray());
        AlgorithmPixelRoi roi = AlgorithmPixelRoi.Create(new RectangleAlgorithmRoi(origin, origin, 3 - origin, 2 - origin), image);
        byte[] mask = new byte[6];

        BinaryAnalysisMaskSummary summary = BinaryAnalysisMaskBuilder.Build(image, roi, 128, true, mask, default);

        Assert.Equal(new BinaryAnalysisMaskSummary(included, foreground, 0), summary);
        Assert.Equal(origin == 0 ? new byte[] { 0, 255, 255, 0, 255, 255 } : new byte[] { 0, 0, 0, 0, 255, 255 }, mask);
    }
}
