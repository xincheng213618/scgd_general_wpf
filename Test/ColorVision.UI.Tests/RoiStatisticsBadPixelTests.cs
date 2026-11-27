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
    [Fact]
    public async Task LargeRoiKeepsTheGlobalStrongestCandidatesAndBothCounts()
    {
        const int width = 1025, height = 513, stride = width * 6 + 3;
        byte[] pixels = new byte[stride * height];
        Spike(13, 31, 2, ushort.MaxValue);
        Spike(13, 31, 0, 42000);
        Spike(25, 89, 1, 50000);
        Spike(17, 204, 0, 65000);
        Spike(11, 310, 2, ushort.MaxValue);
        Spike(31, 410, 0, 40000);

        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Bgr48, pixels);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RoiStatistics,
                new RoiStatisticsParameters { MaximumBadPixelCandidates = 3 }, new RectangleAlgorithmRoi(0, 0, width, height)),
            Inputs = [new AlgorithmInput { Name = "source", Image = image }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var candidates = result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!;
        Assert.Equal([(13, 31, 2), (11, 310, 2), (17, 204, 0)], candidates.Rows.Select(row =>
            (row["X"].GetInt32(), row["Y"].GetInt32(), row["Channel"].GetInt32())));
        var summary = result.GetArtifact<AlgorithmTableArtifact>("roi-statistics-summary")!;
        Assert.Equal([3, 1, 2], summary.Rows.Select(row => row["BadPixelCandidateCount"].GetInt32()));
        var measurements = result.GetArtifact<AlgorithmMeasurementArtifact>("roi-statistics")!.Measurements;
        Assert.Equal(5, Assert.Single(measurements, measurement => measurement.Name == "roi.bad_pixel_candidate_count").Value);

        void Spike(int x, int y, int channel, ushort value)
            => BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(y * stride + x * 6 + channel * 2), value);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LargeUnorderedFloatRoiMatchesSortedPercentileRanks(bool mixedZeroSamples, bool negativeZeroPercentile)
    {
        const int width = 183, height = 103, stride = width * sizeof(float) + 3;
        byte[] pixels = new byte[stride * height];
        List<float> finite = new(width * height);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            float value = (index % 99) switch
            {
                0 => float.NaN,
                1 => float.PositiveInfinity,
                2 => float.NegativeInfinity,
                _ => ((y * 161 + x * 91) % 1431 - 715) * .25f,
            };
            if (mixedZeroSamples && float.IsFinite(value) && value < 30)
                value = index == 3 ? 1f : index % 2 == 0 ? 0f : -0f;
            BinaryPrimitives.WriteSingleLittleEndian(pixels.AsSpan(y * stride + x * sizeof(float)), value);
            if (float.IsFinite(value)) finite.Add(value);
        }
        finite.Sort();
        double[] percentiles = [97.25, 0, 95.125, 25, 25.001, 50, 100, 99.999];
        if (negativeZeroPercentile) percentiles[1] = BitConverter.Int64BitsToDouble(long.MinValue);
        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Gray32Float, pixels);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RoiStatistics,
                new RoiStatisticsParameters { Percentiles = percentiles, HistogramBins = 31, DetectBadPixelCandidates = false },
                new RectangleAlgorithmRoi(0, 0, width, height)),
            Inputs = [new AlgorithmInput { Name = "source", Image = image }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var summary = result.GetArtifact<AlgorithmTableArtifact>("roi-statistics-summary")!;
        var row = Assert.Single(summary.Rows);
        long[] expected = percentiles.Select(percentile =>
        {
            double rank = percentile / 100 * (finite.Count - 1);
            int lower = (int)Math.Floor(rank), upper = (int)Math.Ceiling(rank);
            double value = finite[lower] + ((double)finite[upper] - finite[lower]) * (rank - lower);
            return BitConverter.DoubleToInt64Bits(value);
        }).ToArray();
        Assert.Equal(expected, summary.Columns.TakeLast(percentiles.Length)
            .Select(column => BitConverter.DoubleToInt64Bits(row[column.Name].GetDouble())));
        Assert.Equal(expected, result.GetArtifact<AlgorithmMeasurementArtifact>("roi-statistics")!.Measurements
            .Where(measurement => measurement.Name == "channel.percentile").Select(measurement => BitConverter.DoubleToInt64Bits(measurement.Value)));
        Assert.Equal(finite.Count, row["ValidCount"].GetInt32());
        Assert.Equal(width * height - finite.Count, row["InvalidCount"].GetInt32());
        Assert.Equal((long)finite.Count, result.GetArtifact<AlgorithmTableArtifact>("roi-histogram")!.Rows.Sum(bin => bin["Count"].GetInt64()));
    }

    [Fact]
    public async Task UnsortedPercentilesInterpolateAcrossUnoccupiedGray16Bins()
    {
        using AlgorithmImageBuffer image = new(2, 1, 4, AlgorithmImageFormat.Gray16, [0, 0, 255, 255]);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RoiStatistics,
                new RoiStatisticsParameters { Percentiles = [75, 0, 25, 100, 50], DetectBadPixelCandidates = false },
                new RectangleAlgorithmRoi(0, 0, 2, 1)),
            Inputs = [new AlgorithmInput { Name = "source", Image = image }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmTableArtifact table = result.GetArtifact<AlgorithmTableArtifact>("roi-statistics-summary")!;
        Assert.Equal(new[] { "P75", "P0", "P25", "P100", "P50" }, table.Columns.TakeLast(5).Select(column => column.Name));
        var row = Assert.Single(table.Rows);
        double[] expected = [49151.25, 0, 16383.75, 65535, 32767.5];
        Assert.Equal(expected, table.Columns.TakeLast(5).Select(column => row[column.Name].GetDouble()));
        Assert.Equal(expected, result.GetArtifact<AlgorithmMeasurementArtifact>("roi-statistics")!.Measurements
            .Where(measurement => measurement.Name == "channel.percentile").Select(measurement => measurement.Value));
    }

    [Fact]
    public async Task IntegerRectangleMatchesTheSamePixelsInAPolygonWithPaddedRows()
    {
        const int width = 15, height = 13, stride = width * 6 + 3;
        byte[] data = new byte[stride * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int offset = y * stride + x * 6;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), (ushort)(x == 4 && y == 3 ? ushort.MaxValue : 83 * x + 167 * y));
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 2), (ushort)(x == 5 && y == 4 ? 0 : 331 * x + 13 * y));
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 4), 30000);
        }
        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Bgr48, data);
        using AlgorithmResult rectangle = await Run(new RectangleAlgorithmRoi(1, 1, 11, 9));
        using AlgorithmResult polygon = await Run(new PolygonAlgorithmRoi([new(.5, .5), new(11.5, .5), new(11.5, 9.5), new(.5, 9.5)]));

        Assert.Equal(AlgorithmResultStatus.Succeeded, rectangle.Status);
        Assert.Equal(rectangle.Status, polygon.Status);
        Assert.Equal(rectangle.Artifacts.Where(IsNumeric).Select(Serialize), polygon.Artifacts.Where(IsNumeric).Select(Serialize));

        ValueTask<AlgorithmResult> Run(AlgorithmRoi roi) => ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RoiStatistics, new RoiStatisticsParameters(), roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = image }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

        static bool IsNumeric(AlgorithmArtifact artifact) => artifact is AlgorithmMeasurementArtifact or AlgorithmTableArtifact;
        static string Serialize(AlgorithmArtifact artifact) => JsonSerializer.Serialize(artifact, artifact.GetType(), AlgorithmJson.Options);
    }

    [Theory]
    [InlineData("rectangle")]
    [InlineData("integer rectangle")]
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
            "integer rectangle" => new RectangleAlgorithmRoi(4, 3, 6, 5),
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
    public async Task ConcavePolygonKeepsItsClippedEdgesAndDisjointRowRuns()
    {
        const int width = 80, height = 53, stride = width * 2 + 3;
        byte[] data = new byte[stride * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool bright = (x, y) is (0, 6) or (26, 21) or (26, 22) or (53, 47);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(y * stride + x * 2), bright ? ushort.MaxValue : (ushort)12000);
            }

        const double top = 6 + 5e-12;
        var roi = new PolygonAlgorithmRoi([
            new(-4.25, top), new(70, top), new(70, 47), new(53, 47), new(53, 21),
            new(25, 21), new(25, 21), new(25, 47), new(-4.25, 47),
        ]);
        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Gray16, data);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RoiStatistics, new RoiStatisticsParameters(), roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = image }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics.Messages, message => message.Code == "roi_clipped");
        var metrics = result.GetArtifact<AlgorithmMeasurementArtifact>("roi-statistics")!.Measurements;
        Assert.Equal(2280, metrics.Single(value => value.Name == "roi.pixel_count").Value);
        Assert.Equal(3, metrics.Single(value => value.Name == "channel.high_saturated_count").Value);
        var candidates = result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!.Rows
            .Select(row => (X: row["X"].GetInt32(), Y: row["Y"].GetInt32())).OrderBy(point => point.X);
        Assert.Equal(new[] { (0, 6), (26, 21), (53, 47) }, candidates);
    }

    [Fact]
    public async Task ClippedPhysicalCircleKeepsTheIncludedTangentPixelAndItsNeighbors()
    {
        const int width = 70, height = 55, stride = width * 2 + 3;
        const double dpiX = 120, dpiY = 180;
        byte[] data = new byte[stride * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool bright = (x, y) is (10, 26) or (10, 25) or (50, 26);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(y * stride + x * 2), bright ? ushort.MaxValue : (ushort)12000);
            }

        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Gray16, data, dpiX, dpiY);
        var roi = new CircleAlgorithmRoi(new(30 * 25.4 / dpiX, 26 * 25.4 / dpiY), (20 - 4e-9) * 25.4 / dpiX)
            { CoordinateSpace = AlgorithmCoordinateSpace.Physical };
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RoiStatistics, new RoiStatisticsParameters(), roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = image }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics.Messages, message => message.Code == "roi_clipped");
        var metrics = result.GetArtifact<AlgorithmMeasurementArtifact>("roi-statistics")!.Measurements;
        Assert.Equal(1824, metrics.Single(value => value.Name == "roi.pixel_count").Value);
        Assert.Equal(1, metrics.Single(value => value.Name == "channel.high_saturated_count").Value);
        var candidate = Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!.Rows);
        Assert.Equal(10, candidate["X"].GetInt32());
        Assert.Equal(26, candidate["Y"].GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IntegerInteriorChecksBothMiddleNeighborsAtMinimumDeviation(bool gray16)
    {
        const int width = 8, height = 3;
        int bytesPerPixel = gray16 ? 2 : 1;
        int stride = width * bytesPerPixel + 3;
        int scale = gray16 ? 257 : 1;
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int sample = (x, y) switch
            {
                (2, 1) => 30,
                (4 or 5 or 6, 0) or (4 or 5, 1) => 60,
                _ => 20,
            };
            int offset = y * stride + x * bytesPerPixel;
            if (gray16) BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(offset, 2), (ushort)(sample * scale));
            else pixels[offset] = (byte)sample;
        }

        using AlgorithmImageBuffer image = new(width, height, stride,
            gray16 ? AlgorithmImageFormat.Gray16 : AlgorithmImageFormat.Gray8, pixels);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RoiStatistics,
                new RoiStatisticsParameters { BadPixelMinimumDeviationFraction = 10d / byte.MaxValue, BadPixelSigmaThreshold = 0.1 },
                new RectangleAlgorithmRoi(0, 0, width, height)),
            Inputs = [new AlgorithmInput { Name = "source", Image = image }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var candidates = result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!.Rows;
        Assert.DoesNotContain(candidates, row => row["X"].GetInt32() == 2 && row["Y"].GetInt32() == 1);
        var candidate = Assert.Single(candidates, row => row["X"].GetInt32() == 5 && row["Y"].GetInt32() == 1);
        Assert.Equal(40 * scale, candidate["LocalMedian"].GetDouble());
        Assert.Equal(20 * scale, candidate["Deviation"].GetDouble());
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
