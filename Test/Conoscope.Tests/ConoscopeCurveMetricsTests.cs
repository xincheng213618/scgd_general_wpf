using Conoscope.Core;

namespace Conoscope.Tests;

public sealed class ConoscopeCurveMetricsTests
{
    [Fact]
    public void AsymmetricPeakUsesInterpolatedCrossingsWithoutBaselineSubtraction()
    {
        var metrics = ConoscopeCurveMetrics.Measure([-3, -1, 0, 2, 5], [2, 8, 10, 6, 2]);
        Assert.Equal(ConoscopeFwhmStatus.Available, metrics.Status);
        Assert.Equal(0, metrics.PeakAngle);
        Assert.Equal(-2, metrics.LeftHalfMaximum);
        Assert.Equal(2.75, metrics.RightHalfMaximum);
        Assert.Equal(4.75, metrics.Width);
    }

    [Fact]
    public void SideLobesDoNotExpandMainLobeWidth()
    {
        var metrics = ConoscopeCurveMetrics.Measure([-4, -3, -2, -1, 0, 1, 2, 3, 4], [0, 8, 0, 5, 10, 5, 0, 8, 0]);
        Assert.Equal(2, metrics.Width);
        Assert.True(metrics.HasMultipleLobes);
    }

    [Fact]
    public void TiedPeaksSelectNearestZeroThenSmallerAngleAndHandleFlatTop()
    {
        var metrics = ConoscopeCurveMetrics.Measure([-3, -2, -1, 0, 1, 2, 3], [0, 10, 10, 10, 10, 10, 0]);
        Assert.Equal(0, metrics.PeakAngle);
        Assert.Equal(5, metrics.Width);
        metrics = ConoscopeCurveMetrics.Measure([-2, -1, 0, 1, 2], [0, 10, 0, 10, 0]);
        Assert.Equal(-1, metrics.PeakAngle);
        Assert.Equal(1, metrics.Width);
    }

    [Fact]
    public void MissingCrossingsAndGapsNeverBecomeFabricatedWidths()
    {
        var truncated = ConoscopeCurveMetrics.Measure([-1, 0, 1], [8, 10, 8]);
        Assert.Equal(ConoscopeFwhmStatus.MissingCrossing, truncated.Status);
        Assert.Null(truncated.Width);
        var gap = ConoscopeCurveMetrics.Measure([-2, -1, 0, 1, 2], [0, double.NaN, 10, 5, 0]);
        Assert.Equal(ConoscopeFwhmStatus.GapAtCrossing, gap.Status);
        Assert.Null(gap.Width);
        Assert.True(gap.HasMissingSamples);
        var outside = ConoscopeCurveMetrics.Measure([-2, -1, 0, 1, 2], [double.NaN, 5, 10, 5, double.NaN]);
        Assert.Equal(2, outside.Width);
        Assert.True(outside.HasMissingSamples);
    }

    [Fact]
    public void InvalidAxisAndNonpositiveSignalAreExplicitlyUnavailable()
    {
        Assert.Equal(ConoscopeFwhmStatus.InvalidAxis, ConoscopeCurveMetrics.Measure([0, 0, 1], [0, 1, 0]).Status);
        Assert.Equal(ConoscopeFwhmStatus.InvalidAxis, ConoscopeCurveMetrics.Measure([0, 1], [0, 1]).Status);
        Assert.Equal(ConoscopeFwhmStatus.InvalidAxis, ConoscopeCurveMetrics.Measure([0, double.NaN, 2], [0, 1, 0]).Status);
        Assert.Equal(ConoscopeFwhmStatus.NoPositivePeak, ConoscopeCurveMetrics.Measure([-1, 0, 1], [-1, 0, double.NaN]).Status);
    }

    [Fact]
    public void NormalizationCopiesValuesAndPreservesGapsAndWidth()
    {
        double[] raw = [double.NaN, 0, 5, 10, 5, 0, double.PositiveInfinity];
        double[] original = raw.ToArray();
        var normalized = ConoscopeCurveMetrics.NormalizeToPeak(raw)!;
        Assert.Equal(1, normalized[3]);
        Assert.True(double.IsNaN(normalized[0]));
        Assert.True(double.IsNaN(normalized[^1]));
        Assert.Equal(raw, original);
        double[] angles = [-3, -2, -1, 0, 1, 2, 3];
        Assert.Equal(ConoscopeCurveMetrics.Measure(angles, raw).Width, ConoscopeCurveMetrics.Measure(angles, normalized).Width);
        Assert.Null(ConoscopeCurveMetrics.NormalizeToPeak([-1, 0, double.NaN]));
    }

    [Theory]
    [InlineData("polar-diameter-angle", "Y", "cd/m²", true)]
    [InlineData("HorizontalVertical:V-degrees", "Y", "cd/m²", true)]
    [InlineData("NorthPolar:H-degrees", "Y", "cd/m2", true)]
    [InlineData("polar-azimuth-angle", "Y", "cd/m²", false)]
    [InlineData("polar-diameter-angle", "x", "", false)]
    [InlineData("polar-diameter-angle", "Y", "ratio", false)]
    [InlineData("polar-diameter-angle", "Iv", "cd", true)]
    [InlineData("polar-circumference-angle", "Iv", "cd", false)]
    [InlineData("polar-diameter-angle", "Y", "cd", false)]
    public void FwhmIsLimitedToLuminanceIncidenceCurves(string axis, string channel, string unit, bool supported)
    {
        var snapshot = new ConoscopeCurveSnapshot("test", "source", "VA60", "Polar", "cut", "deg", channel, unit,
            [-1, 0, 1], [0, 1, 0], "", axis);
        Assert.Equal(supported, ConoscopeCurveMetrics.SupportsFwhm(snapshot));
    }
}
