using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class ChromaticityDiameterTests
{
    [Theory]
    [InlineData("scattered")]
    [InlineData("collinear")]
    [InlineData("near-collinear")]
    [InlineData("convex")]
    public void MaximumDeltaUvMatchesEveryPairForLargePointSets(string shape)
    {
        const int count = 96;
        Random random = new(1976);
        (double U, double V)[] points = Enumerable.Range(0, count).Select(index => shape switch
        {
            "scattered" => (random.NextDouble(), random.NextDouble()),
            "collinear" => (index / 2048d, 0.25 + index / 4096d),
            "near-collinear" => (index / 2048d, 0.25 + index / 4096d + (index % 3) * Math.ScaleB(1, -50)),
            "convex" => (0.2 + 0.1 * Math.Cos(index * Math.Tau / count), 0.45 + 0.1 * Math.Sin(index * Math.Tau / count)),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        }).ToArray();

        // Shuffle the scan order and include repeated endpoints and interior points.
        points = points.Concat(points.Take(16)).OrderBy(_ => random.Next()).ToArray();
        if (shape == "scattered") points = [.. points, (double.NaN, -0.2), (0.99, double.NaN)];
        Assert.Equal(BruteForceMaximum(points), CIExyuvStatistics.Calculate(CreateResults(points)).ColorUniformityDeltaUv);
    }

    [Theory]
    [InlineData(double.NaN, 0.4, 0.5, 0.6)]
    [InlineData(double.PositiveInfinity, 0.4, 0.5, 0.6)]
    [InlineData(double.PositiveInfinity, 0.4, double.PositiveInfinity, 0.6)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity, 0.5, double.PositiveInfinity)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity)]
    [InlineData(1e300, 0.4, -1e300, 0.6)]
    [InlineData(1e-180, 2e-180, -1e-180, -2e-180)]
    public void MaximumDeltaUvRetainsLegacyFloatingPointBehavior(double firstU, double firstV, double secondU, double secondV)
    {
        (double U, double V)[] points = [.. Enumerable.Repeat((secondU, secondV), 95), (firstU, firstV)];
        Assert.Equal(BruteForceMaximum(points), CIExyuvStatistics.Calculate(CreateResults(points)).ColorUniformityDeltaUv);
    }

    [Fact]
    public void RoundedCollinearInteriorPairCanExceedBothEndpointPairs()
    {
        (double U, double V) origin = (0, 0);
        (double U, double V) interior = (0.05694667416866184, 0.3843900506384671);
        (double U, double V) firstEnd = (0.05694667416865584, 0.384390050638468);
        (double U, double V) secondEnd = (0.05694667416866783, 0.3843900506384662);
        (double U, double V)[] points =
        [
            origin, interior, firstEnd, secondEnd,
            .. Enumerable.Range(1, 24).Select(index => (interior.U * index / 64d, interior.V * index / 64d)),
        ];

        double expected = BruteForceMaximum([origin, interior]);
        Assert.True(BruteForceMaximum([origin, firstEnd, secondEnd]) < expected);
        Assert.Equal(expected, CIExyuvStatistics.Calculate(CreateResults(points)).ColorUniformityDeltaUv);
    }

    [Fact]
    public void CsvUsesTheSameMaximumDeltaUvAsTheStatisticsPanel()
    {
        (double U, double V)[] points =
        [
            (0.1, 0.2), (0.1, 0.5), (0.5, 0.2), (0.5, 0.5),
            .. Enumerable.Range(1, 32).Select(index => (0.2 + index / 320d, 0.3)),
        ];
        ObservableCollection<PoiResultCIExyuvData> results = CreateResults(points);
        string path = Path.Combine(Path.GetTempPath(), $"ColorVision-chromaticity-{Guid.NewGuid():N}.csv");
        try
        {
            results.SaveCsv(path);

            string row = Assert.Single(File.ReadLines(path), line => line.StartsWith("Color Uniformity(Δuv),", StringComparison.Ordinal));
            double csv = double.Parse(row.Split(',')[1], CultureInfo.InvariantCulture);
            Assert.Equal(BruteForceMaximum(points), csv);
            Assert.Equal(CIExyuvStatistics.Calculate(results).ColorUniformityDeltaUv, csv);
        }
        finally { File.Delete(path); }
    }

    private static ObservableCollection<PoiResultCIExyuvData> CreateResults(IEnumerable<(double U, double V)> points) =>
        new(points.Select(point => new PoiResultCIExyuvData { u = point.U, v = point.V }));

    private static double BruteForceMaximum((double U, double V)[] points)
    {
        double maximum = 0;
        for (int first = 0; first < points.Length; first++)
        {
            for (int second = first + 1; second < points.Length; second++)
            {
                double du = points[first].U - points[second].U;
                double dv = points[first].V - points[second].V;
                double distance = Math.Sqrt(du * du + dv * dv);
                if (distance > maximum) maximum = distance;
            }
        }
        return maximum;
    }
}
