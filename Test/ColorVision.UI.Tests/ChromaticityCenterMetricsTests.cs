using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Tests;

public sealed class ChromaticityCenterMetricsTests
{
    [Fact]
    public void SymmetricSamplesSeparateWhitePointAccuracyFromSpatialUniformity()
    {
        const double du = 0.006;
        const double dv = 0.008;
        ChromaticityCenterMetrics result = ChromaticityCenterCalculator.Calculate(
        [
            (ChromaticityCenterCalculator.D65UPrime - du, ChromaticityCenterCalculator.D65VPrime - dv),
            (ChromaticityCenterCalculator.D65UPrime + du, ChromaticityCenterCalculator.D65VPrime + dv),
        ]);

        Assert.Equal(2, result.SampleCount);
        Assert.Equal(0, result.InvalidSampleCount);
        Assert.Equal(ChromaticityCenterCalculator.D65UPrime, result.AverageUPrime, 12);
        Assert.Equal(ChromaticityCenterCalculator.D65VPrime, result.AverageVPrime, 12);
        Assert.Equal(0, result.CenterDistanceToReference, 12);
        Assert.Equal(0.01, result.SpatialRms, 12);
        Assert.Equal(0.01, result.RmsToReference, 12);
    }

    [Fact]
    public void RmsToReferenceObeysCenterAndSpatialDecompositionAndCountsInvalidSamples()
    {
        ChromaticityCenterMetrics result = ChromaticityCenterCalculator.Calculate(
        [
            (0.20, 0.46),
            (0.21, 0.48),
            (double.NaN, 0.47),
        ]);

        Assert.Equal(2, result.SampleCount);
        Assert.Equal(1, result.InvalidSampleCount);
        Assert.Equal(result.RmsToReference * result.RmsToReference,
            result.CenterDistanceToReference * result.CenterDistanceToReference + result.SpatialRms * result.SpatialRms, 12);
    }

    [Fact]
    public void ExistingCieStatisticsRetainMaximumDeltaUvAndExposeD65Rms()
    {
        var points = new ObservableCollection<PoiResultCIExyuvData>
        {
            new() { Y = 100, u = ChromaticityCenterCalculator.D65UPrime, v = ChromaticityCenterCalculator.D65VPrime },
            new() { Y = 110, u = ChromaticityCenterCalculator.D65UPrime + 0.003, v = ChromaticityCenterCalculator.D65VPrime + 0.004 },
        };

        CIExyuvStatistics result = CIExyuvStatistics.Calculate(points);

        Assert.Equal(0.005, result.ColorUniformityDeltaUv, 12);
        Assert.Equal(Math.Sqrt(0.0000125), result.ColorCenterRmsToD65, 12);
        Assert.Equal(Math.Sqrt(0.00000625), result.ColorCenterDistanceToD65, 12);
        Assert.Equal(Math.Sqrt(0.00000625), result.ChromaticitySpatialRms, 12);
    }
}
