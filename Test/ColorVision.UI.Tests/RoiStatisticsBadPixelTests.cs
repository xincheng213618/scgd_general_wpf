using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;

namespace ColorVision.UI.Tests;

public sealed class RoiStatisticsBadPixelTests
{
    [Fact]
    public async Task MinimumDeviationIsExclusiveAndKeepsStrongerOutliersWithPaddedFloatRows()
    {
        const int width = 8;
        const int height = 3;
        const int stride = width * sizeof(float) + 4;
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            WritePixel(x, y, 0.25f);

        WritePixel(0, 0, float.NaN);
        WritePixel(1, 1, 0.5f);
        WritePixel(4, 0, 0);
        WritePixel(5, 0, 0.5f);
        WritePixel(5, 1, 0.75f);

        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Gray32Float, pixels);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = new AlgorithmInvocation
            {
                AlgorithmId = StandardAlgorithmIds.RoiStatistics,
                Roi = new RectangleAlgorithmRoi(0, 0, width, height),
                Parameters = AlgorithmJson.ToElement(new RoiStatisticsParameters { BadPixelMinimumDeviationFraction = 0.25 }),
            },
            Inputs = [new AlgorithmInput { Name = "source", Image = image }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var candidate = Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!.Rows);
        Assert.Equal(5, candidate["X"].GetInt32());
        Assert.Equal(1, candidate["Y"].GetInt32());
        Assert.Equal(0.25, candidate["LocalMedian"].GetDouble());
        Assert.Equal(0.5, candidate["Deviation"].GetDouble());
        Assert.Equal(0.25, candidate["Threshold"].GetDouble());

        void WritePixel(int x, int y, float value)
            => BinaryPrimitives.WriteSingleLittleEndian(pixels.AsSpan(y * stride + x * sizeof(float)), value);
    }
}
