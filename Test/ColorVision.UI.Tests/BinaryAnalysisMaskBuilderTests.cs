using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class BinaryAnalysisMaskBuilderTests
{
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
