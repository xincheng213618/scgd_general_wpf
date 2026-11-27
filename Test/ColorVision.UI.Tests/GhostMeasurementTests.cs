using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class GhostMeasurementTests
{
    [Fact]
    public async Task ThresholdEqualityAndPrimaryBoundaryPreserveCandidateAndOutsideEnergy()
    {
        const int width = 65, height = 73, stridePixels = width + 3;
        float[] pixels = new float[stridePixels * height];
        Array.Fill(pixels, .03125f);
        for (int y = 18; y < 54; y++)
            Array.Fill(pixels, .5625f, y * stridePixels + 16, 32);
        pixels[19 * stridePixels + 17] = .8125f;

        Array.Fill(pixels, .3125f, 3 * stridePixels + 4, 5);
        Array.Fill(pixels, .25f, 31 * stridePixels + 48, 2);
        Array.Fill(pixels, .25f, 32 * stridePixels + 48, 2);
        pixels[72 * stridePixels + 62] = 1;
        pixels[70 * stridePixels + 26] = .125f;

        using AlgorithmImageBuffer input = new(width, height, stridePixels * sizeof(float), AlgorithmImageFormat.Gray32Float,
            MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.Ghost, new GhostMeasurementParameters
            {
                PrimaryX = .25, PrimaryY = .25, PrimaryWidth = .5, PrimaryHeight = .5,
                Background = .0625, RelativeThreshold = .25, MinimumArea = 3,
            }),
            Inputs = [new AlgorithmInput { Image = input, Name = "source", ColorSpace = "sensor" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var rows = result.GetArtifact<AlgorithmTableArtifact>("stray-light-candidates")!.Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal("streak_candidate", rows[0]["kind"].GetString());
        Assert.Equal(5, rows[0]["area_px2"].GetInt32());
        Assert.Equal(6, rows[0]["x_px"].GetDouble());
        Assert.Equal("connected_flare_candidate", rows[1]["kind"].GetString());
        Assert.Equal(4, rows[1]["area_px2"].GetInt32());
        Assert.Equal(48.5, rows[1]["x_px"].GetDouble());
        Assert.Equal(.25, rows[1]["peakOverPrimaryPeak"].GetDouble());
        Assert.Equal(.75 / 576.25, rows[1]["energyOverPrimaryRegion"].GetDouble());

        var metrics = result.GetArtifact<AlgorithmMeasurementArtifact>("summary-1")!.Measurements;
        Assert.Equal(3 / 576.25, metrics.Single(value => value.Name == "outside_integral_over_primary_region").Value);
        Assert.Equal(3d / (width * height - 32 * 36) / .75, metrics.Single(value => value.Name == "outside_mean_over_primary_peak").Value);
    }
}
