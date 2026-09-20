using Conoscope.ApplicationServices.Analysis;
using Conoscope.Core;
using System.Windows;

namespace Conoscope.Tests;

public sealed class ConoscopeAngularAnalysisTests
{
    [Fact]
    public void CutsRespectAzimuthAndSignedIncidenceAndBilinearSampling()
    {
        // A linear image has an exact analytical bilinear result at every subpixel position.
        var curves = ConoscopeAngularAnalysis.Analyze(Source((x, y) => 100 + 2 * x + 3 * y));
        Assert.Equal(5, curves.Count);
        foreach (var curve in curves.Take(4))
        {
            double phi = curve.AzimuthDegrees!.Value * Math.PI / 180;
            for (int i = 0; i < curve.Values.Count; i++)
                Assert.Equal(150 + curve.Positions[i] * (2 * Math.Cos(phi) - 3 * Math.Sin(phi)), curve.Values[i], 10);
        }
        Assert.All(curves[^1].Values, value => Assert.Equal(150, value, 10));
    }

    [Fact]
    public void FullAzimuthMeanIncludesSectorsBetweenTheFourCuts()
    {
        // This bright patch is between the four diameters, so their average cannot find it.
        var curves = ConoscopeAngularAnalysis.Analyze(Source((x, y) => x == 13 && y == 9 ? 1000 : 10));
        Assert.All(curves.Take(4), curve => Assert.Equal(10, curve.Values[^1], 9));
        Assert.True(curves[^1].Values[^1] > 10);
        Assert.Equal(curves[^1].Values, curves[^1].Values.Reverse());
    }

    [Theory]
    [InlineData(3, 61)]
    [InlineData(2.35, 49)]
    [InlineData(0.03, 3)]
    public void IncludesZeroAndExactEndpointsWithoutDuplicatePositions(double maximum, int expectedCount)
    {
        var curve = ConoscopeAngularAnalysis.Analyze(Source((_, _) => 12, maximum))[^1];
        Assert.Equal(expectedCount, curve.Positions.Count);
        Assert.Equal(-maximum, curve.Positions[0]);
        Assert.Equal(maximum, curve.Positions[^1]);
        Assert.Single(curve.Positions, x => x == 0);
        Assert.All(curve.Positions.Zip(curve.Positions.Skip(1)), pair => Assert.True(pair.Second > pair.First));
    }

    [Fact]
    public void OutOfImageAndMissingSectorsStayMissingInsteadOfBecomingPartialMeans()
    {
        var outside = ConoscopeAngularAnalysis.Analyze(Source((_, _) => 12, maximum: 11));
        Assert.True(double.IsNaN(outside[0].Values[^1]));
        Assert.True(double.IsNaN(outside[^1].Values[^1]));
        var hole = ConoscopeAngularAnalysis.Analyze(Source((x, y) => x == 13 && y == 9 ? double.NaN : 12));
        Assert.True(double.IsNaN(hole[^1].Values[^1]));
        Assert.Equal(12, hole[0].Values[^1]);
        Assert.Equal(12, hole[^1].Values[hole[^1].Values.Count / 2]);
    }

    [Fact]
    public void CancellationAndInvalidGeometryDoNotStartSampling()
    {
        int reads = 0;
        var source = Source((_, _) => { reads++; return 1; });
        Assert.Throws<OperationCanceledException>(() => ConoscopeAngularAnalysis.Analyze(source, new CancellationToken(true)));
        Assert.Throws<ArgumentException>(() => ConoscopeAngularAnalysis.Analyze(Source((_, _) => { reads++; return 1; }, maximum: double.NaN)));
        Assert.Equal(0, reads);
    }

    private static ConoscopeExportContext Source(Func<int, int, double> luminance, double maximum = 3) => new()
    {
        ModelName = "Synthetic", ImageWidth = 21, ImageHeight = 21, Center = new Point(10, 10),
        MaxAngle = maximum, PixelsPerDegree = 1, ReadXyz = (x, y) => new(0, luminance(x, y), 0)
    };
}
