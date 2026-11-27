using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;

namespace ColorVision.UI.Tests;

public sealed class DustDetectionTests
{
    [Fact]
    public async Task FullFrameApertureCountsDistanceFromThePhysicalImageEdges()
    {
        const int width = 95, height = 73, stride = width + 5, margin = 11;
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
            pixels.AsSpan(y * stride, width).Fill(190);

        using var input = new AlgorithmImageBuffer(width, height, stride, AlgorithmImageFormat.Gray8, pixels);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.Dust,
                new DustDetectionParameters { BorderMargin = margin, BackgroundKernel = 121 }),
            Inputs = [new AlgorithmInput { Name = "source", Image = input }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer mask = result.GetArtifact<AlgorithmImageArtifact>("有效成像区域")!.Image;
        Assert.Equal((width, height), (mask.Width, mask.Height));

        byte[] expected = new byte[width * height];
        for (int y = margin; y < height - margin; y++)
            expected.AsSpan(y * width + margin, width - 2 * margin).Fill(byte.MaxValue);
        Assert.Equal(expected, mask.Data.ToArray());
    }

    [Fact]
    public async Task BackgroundNearMaskBoundaryUsesTheFullImageNeighborhood()
    {
        const int width = 331, height = 273, stride = width + 3;
        const int strongX = 98, strongY = 90, weakX = 79, weakY = 150;
        var pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int value = x is >= 57 and <= 267 && y is >= 37 and <= 239 ? 190 : 5;
            if (InsideSpot(x - strongX, y - strongY)) value -= 65;
            if (InsideSpot(x - weakX, y - weakY)) value -= 18;
            pixels[y * stride + x] = (byte)value;
        }

        using var image = new AlgorithmImageBuffer(width, height, stride, AlgorithmImageFormat.Gray8, pixels);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.Dust,
                new DustDetectionParameters { BorderMargin = 12, ContrastPercent = 8 }),
            Inputs = [new AlgorithmInput { Name = "source", Image = image }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        // The dim aperture exterior lowers the full-image background around the weak spot.
        // Reflecting at the valid mask bounds would incorrectly report both spots.
        var row = Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("灰尘候选")!.Rows);
        double left = row["原图X_px"].GetDouble(), top = row["原图Y_px"].GetDouble();
        Assert.True(left <= strongX && strongX < left + row["原图宽_px"].GetDouble());
        Assert.True(top <= strongY && strongY < top + row["原图高_px"].GetDouble());

        AlgorithmImageBuffer mask = result.GetArtifact<AlgorithmImageArtifact>("灰尘候选掩膜")!.Image;
        Assert.Equal((width, height), (mask.Width, mask.Height));
        Assert.Equal(byte.MaxValue, mask.Data.Span[strongY * mask.Stride + strongX]);
        Assert.Equal(0, mask.Data.Span[weakY * mask.Stride + weakX]);

        static bool InsideSpot(int dx, int dy) => dx * dx + dy * dy <= 25;
    }
}
