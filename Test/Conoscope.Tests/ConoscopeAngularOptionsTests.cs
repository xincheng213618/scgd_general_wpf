using Conoscope.ApplicationServices.Analysis;
using Conoscope.Core;
using System.Windows;

namespace Conoscope.Tests;

public sealed class ConoscopeAngularOptionsTests
{
    [Theory]
    [InlineData(1, 90, -2, 2)]
    [InlineData(2, 90, 2, -2)]
    [InlineData(3, 0, 2, -2)]
    [InlineData(4, 0, -2, 2)]
    public void MirrorReplacesOnlyTargetSideAndLeavesSymmetryAxisIntact(int direction, double azimuth, double target, double reference)
    {
        var source = Source((x, y) => 1000 + x + 3 * y);
        var raw = ConoscopeAngularAnalysis.Analyze(source);
        var curves = ConoscopeAngularAnalysis.Analyze(source, options: new() { MirrorDirection = (ConoscopeMirrorDirection)direction });
        var before = raw.Single(c => c.AzimuthDegrees == azimuth);
        var after = curves.Single(c => c.AzimuthDegrees == azimuth);
        Assert.Equal(At(before, reference), At(after, target), 9);
        Assert.Equal(At(before, reference), At(after, reference));
        Assert.Equal(At(before, 0), At(after, 0));
        Assert.True(after.MirroredRaySamples > 0);
        Assert.Equal(0, after.UnavailableMirrorRaySamples);
    }

    [Fact]
    public void RectangleRestrictsReplacementAndReferenceCanRemainMissing()
    {
        var options = new ConoscopeAngularAnalysisOptions
        {
            MirrorDirection = ConoscopeMirrorDirection.RightToLeft,
            MirrorRegion = new(-3, -1, -1, 1)
        };
        var source = Source((x, y) => x == 12 && y == 10 ? double.NaN : x);
        var curves = ConoscopeAngularAnalysis.Analyze(source, options: options);
        var cut = curves[0];
        Assert.True(double.IsNaN(At(cut, -2)));
        Assert.Equal(13, At(cut, -3), 9);
        Assert.Equal(9.5, At(cut, -0.5), 9);
        Assert.True(cut.UnavailableMirrorRaySamples > 0);
        Assert.Contains(curves[^1].Values, double.IsNaN);
    }

    [Fact]
    public void PartialMirrorMeanSamplesTheModifiedFieldRatherThanFourCutAverages()
    {
        // Original field: Y=100+x; replace all x>0 by x<0. Ring mean becomes 110-r*mean(|cos(phi)|).
        var source = Source((x, _) => 100 + x);
        var curves = ConoscopeAngularAnalysis.Analyze(source, options: new() { MirrorDirection = ConoscopeMirrorDirection.LeftToRight });
        double expected = 110 - 3 * Enumerable.Range(0, 360).Average(phi => Math.Abs(Math.Cos(phi * Math.PI / 180)));
        Assert.Equal(expected, curves[^1].Values[^1], 10);
        Assert.NotEqual(110, curves[^1].Values[^1]);
        Assert.Equal(curves[^1].Values[0], curves[^1].Values[^1]);
    }

    [Fact]
    public void UniformPlanarLuminanceProducesCosineIntensityAndKnownFwhm()
    {
        var source = new ConoscopeExportContext
        {
            ModelName = "Uniform", Center = new Point(100, 100), ImageWidth = 201, ImageHeight = 201,
            MaxAngle = 90, PixelsPerDegree = 1, ReadXyz = (_, _) => new(0, 1000, 0)
        };
        var options = new ConoscopeAngularAnalysisOptions { Quantity = ConoscopeAngularQuantity.LuminousIntensity, DiameterMillimeters = 2 };
        var cut = ConoscopeAngularAnalysis.Analyze(source, options: options)[0];
        Assert.Equal(Math.PI * 0.001, At(cut, 0), 12);
        Assert.Equal(At(cut, 0) * 0.5, At(cut, 60), 12);
        Assert.Equal(At(cut, 60), At(cut, -60));
        Assert.Equal(0, At(cut, 90));
        Assert.Equal(120, ConoscopeCurveMetrics.Measure(cut.Positions, cut.Values).Width!.Value, 8);
    }

    [Fact]
    public void AreaChangesAbsoluteIntensityButNotPeakNormalizedShape()
    {
        var options = new ConoscopeAngularAnalysisOptions
        {
            Quantity = ConoscopeAngularQuantity.LuminousIntensity, SizeMode = ConoscopeEmitterSizeMode.Area, AreaSquareMillimeters = 1
        };
        var first = ConoscopeAngularAnalysis.Analyze(Source((x, y) => x + y), options: options)[0];
        var second = ConoscopeAngularAnalysis.Analyze(Source((x, y) => x + y), options: options with { AreaSquareMillimeters = 4 })[0];
        for (int i = 0; i < first.Values.Count; i++) Assert.Equal(first.Values[i] * 4, second.Values[i]);
        Assert.Equal(ConoscopeCurveMetrics.NormalizeToPeak(first.Values), ConoscopeCurveMetrics.NormalizeToPeak(second.Values));
        Assert.Equal(1e-6, options.AreaSquareMeters);
    }

    [Fact]
    public void MirrorAndIntensityComposeAndDoNotTurnMissingReferenceIntoZero()
    {
        var source = Source((x, y) => x == 12 && y == 10 ? double.NaN : 100 + x);
        var options = new ConoscopeAngularAnalysisOptions { MirrorDirection = ConoscopeMirrorDirection.RightToLeft };
        var luminance = ConoscopeAngularAnalysis.Analyze(source, options: options)[0];
        var intensity = ConoscopeAngularAnalysis.Analyze(source, options: options with
        { Quantity = ConoscopeAngularQuantity.LuminousIntensity, SizeMode = ConoscopeEmitterSizeMode.Area, AreaSquareMillimeters = 1 })[0];
        Assert.True(double.IsNaN(At(intensity, -2)));
        Assert.Equal(At(luminance, -3) * 1e-6 * Math.Cos(3 * Math.PI / 180), At(intensity, -3), 12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    public void InvalidActiveDiameterIsRejectedBeforeReadingSource(double diameter)
    {
        int reads = 0;
        Assert.Throws<ArgumentException>(() => ConoscopeAngularAnalysis.Analyze(Source((_, _) => { reads++; return 1; }),
            options: new() { Quantity = ConoscopeAngularQuantity.LuminousIntensity, DiameterMillimeters = diameter }));
        Assert.Equal(0, reads);
    }

    [Fact]
    public void InvalidRectanglesAreRejectedWhileDisabledSettingsCannotChangeBaseline()
    {
        foreach (var region in new ConoscopeAngularRegion[] { new(1, 1, -1, 1), new(-1, 1, -1, 1), new(0, 4, -1, 1), new(2.5, 3, 2.5, 3), new(double.NaN, 2, -1, 1) })
            Assert.Throws<ArgumentException>(() => new ConoscopeAngularAnalysisOptions { MirrorDirection = ConoscopeMirrorDirection.LeftToRight, MirrorRegion = region }.Validate(3));
        var disabled = new ConoscopeAngularAnalysisOptions { DiameterMillimeters = double.NaN, MirrorRegion = new(2, 1, 5, 4) };
        var source = Source((x, y) => Math.Sin(x) + y);
        var before = ConoscopeAngularAnalysis.Analyze(source);
        var after = ConoscopeAngularAnalysis.Analyze(source, options: disabled);
        for (int i = 0; i < before.Count; i++) Assert.Equal(before[i].Values, after[i].Values);
    }

    private static double At(ConoscopeAngularCurve curve, double theta)
        => curve.Values[Enumerable.Range(0, curve.Positions.Count).Single(i => Math.Abs(curve.Positions[i] - theta) < 1e-9)];
    private static ConoscopeExportContext Source(Func<int, int, double> luminance) => new()
    {
        ModelName = "Synthetic", Center = new Point(10, 10), ImageWidth = 21, ImageHeight = 21,
        MaxAngle = 3, PixelsPerDegree = 1, ReadXyz = (x, y) => new(0, luminance(x, y), 0)
    };
}
