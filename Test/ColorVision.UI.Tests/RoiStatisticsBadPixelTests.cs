using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class RoiStatisticsBadPixelTests
{
    [Theory]
    [InlineData("rectangle")]
    [InlineData("physical circle")]
    [InlineData("clipped polygon")]
    public async Task DisplayRegionKeepsStatisticsAndBadPixelCoordinatesInTheFullImage(string shape)
    {
        const int width = 13, height = 9, stride = width * 6 + 2;
        const double dpiX = 120, dpiY = 144;
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Span<byte> pixel = pixels.AsSpan(y * stride + x * 6, 6);
            BinaryPrimitives.WriteUInt16LittleEndian(pixel, x == 6 && y == 4 ? ushort.MaxValue : (ushort)(150 + x + y));
            BinaryPrimitives.WriteUInt16LittleEndian(pixel[2..], 500);
            BinaryPrimitives.WriteUInt16LittleEndian(pixel[4..], 900);
        }

        AlgorithmRoi roi = shape switch
        {
            "rectangle" => new RectangleAlgorithmRoi(3.25, 2.25, 6, 5),
            "physical circle" => new CircleAlgorithmRoi(new(6 * 25.4 / dpiX, 4 * 25.4 / dpiY), 0.48)
                { CoordinateSpace = AlgorithmCoordinateSpace.Physical },
            _ => new PolygonAlgorithmRoi([new(4, -1), new(10, 3), new(9, 7), new(4, 7)]),
        };
        ImageSelectionScope scope = new(Guid.NewGuid(), 17, width, height, dpiX, dpiY);
        AlgorithmPixelRoi region = Assert.IsType<AlgorithmPixelRoi>(RoiStatisticsAlgorithmProvider.GetDisplayRegion(roi, scope));
        Int32Rect bounds = new(region.MinimumX, region.MinimumY,
            region.MaximumXExclusive - region.MinimumX, region.MaximumYExclusive - region.MinimumY);
        var (full, cropped) = WpfTestHost.Invoke(() =>
        {
            BitmapSource bitmap = BitmapSource.Create(width, height, dpiX, dpiY, PixelFormats.Rgb48, null, pixels, stride);
            return (ImageAlgorithmInputFactory.Copy(bitmap), ImageAlgorithmInputFactory.Copy(bitmap, bounds));
        });
        using (full)
        using (cropped)
        {
            Assert.Equal(bounds.Width * bounds.Height * 6, cropped.Data.Length);
            Assert.True(cropped.Data.Length < full.Data.Length);
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RoiStatistics, new RoiStatisticsParameters(), roi);
            using AlgorithmResult expected = await ImageAlgorithmPlatform.Runner.RunAsync(Request(full));
            using AlgorithmResult actual = await RoiStatisticsAlgorithmProvider.CreateDisplayRegionRunner(ImageAlgorithmPlatform.Runtime, region, scope)
                .RunAsync(Request(cropped));

            Assert.Equal(AlgorithmResultStatus.Succeeded, actual.Status);
            Assert.Equal(expected.Diagnostics.ProviderId, actual.Diagnostics.ProviderId);
            Assert.Equal(expected.Artifacts.Select(Serialize), actual.Artifacts.Select(Serialize));
            Assert.Contains(actual.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!.Rows,
                row => row["X"].GetInt32() == 6 && row["Y"].GetInt32() == 4);

            AlgorithmRunRequest Request(AlgorithmImageBuffer image) => new()
            {
                Invocation = invocation,
                Inputs = [new AlgorithmInput { Name = "source", Image = image }],
                RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
            };
        }

        static string Serialize(AlgorithmArtifact artifact) => JsonSerializer.Serialize(artifact, artifact.GetType(), AlgorithmJson.Options);
    }

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
