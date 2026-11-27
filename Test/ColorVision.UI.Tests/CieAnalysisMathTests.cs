using ColorVision.ImageEditor.Cie;

namespace ColorVision.UI.Tests;

public sealed class CieAnalysisMathTests
{
    [Theory]
    [InlineData(2007)]
    [InlineData(6507)]
    [InlineData(24507)]
    public void EstimateCctFindsSamplesAcrossTheLocus(int temperature)
    {
        var xy = CieColorConverter.CctToApproximatePlanckianXy(temperature);

        var estimate = CieAnalysisMath.EstimateCct(xy);

        Assert.Equal((double)temperature, estimate.TemperatureKelvin, 9);
        Assert.Equal(0, estimate.Duv, 12);
    }

    [Theory]
    [InlineData(1667)]
    [InlineData(25000)]
    public void EstimateCctRejectsProjectionsAtEitherLocusEndpoint(int temperature)
    {
        var xy = CieColorConverter.CctToApproximatePlanckianXy(temperature);

        Assert.False(CieAnalysisMath.EstimateCct(xy).IsFinite);
    }

    [Fact]
    public void EstimateCctKeepsTheDistanceCutoffAroundAnInteriorProjection()
    {
        CieCctResult near = CieAnalysisMath.EstimateCct(new(0.2992992213503914, 0.4211016546004334));
        CieCctResult far = CieAnalysisMath.EstimateCct(new(0.29861018334142014, 0.4258851138993185));

        Assert.True(near.IsFinite);
        Assert.InRange(near.Duv, 0.04899, 0.04901);
        Assert.False(far.IsFinite);
    }
}
