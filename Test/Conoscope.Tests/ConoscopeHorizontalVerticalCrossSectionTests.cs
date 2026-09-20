using Conoscope.Core;
using Point = System.Windows.Point;

namespace Conoscope.Tests;

public sealed class ConoscopeHorizontalVerticalCrossSectionTests
{
    [Theory]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical, 258.0539863588897, 163.40189977777243)]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar, 257.45271203605, 158.17784588202045)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar, 261.1398991172237, 163.78095209284174)]
    public void NonzeroFixedLineUsesTheSelectedCoordinateFormula(ConoscopeCoordinateSystem system, double expectedX, double expectedY)
    {
        ConoscopeExportContext context = Context(width: 401, height: 401, center: new Point(200, 200), scale: 2);
        ConoscopeHorizontalVerticalSample sample = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, system, ConoscopeFixedAxis.Horizontal, 30, 20).Single(item => item.PositionDegrees == 20);
        ConoscopeHorizontalVerticalSample fixedV = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, system, ConoscopeFixedAxis.Vertical, 20, 30).Single(item => item.PositionDegrees == 30);

        Assert.True(sample.IsValid);
        Assert.Equal(30, sample.HorizontalAngle);
        Assert.Equal(20, sample.VerticalAngle);
        // Values independently derived from the three documented inverse direction formulas.
        Assert.Equal(expectedX, sample.SourceX, 9);
        Assert.Equal(expectedY, sample.SourceY, 9);
        Assert.True(fixedV.IsValid);
        Assert.Equal(30, fixedV.HorizontalAngle);
        Assert.Equal(20, fixedV.VerticalAngle);
        Assert.Equal(expectedX, fixedV.SourceX, 9);
        Assert.Equal(expectedY, fixedV.SourceY, 9);
    }

    [Theory]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical)]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar)]
    public void FixedHorizontalIsVerticalAndFixedVerticalIsHorizontal(ConoscopeCoordinateSystem system)
    {
        ConoscopeExportContext context = Context();
        IReadOnlyList<ConoscopeHorizontalVerticalSample> fixedH = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, system, ConoscopeFixedAxis.Horizontal, 0, 30);
        IReadOnlyList<ConoscopeHorizontalVerticalSample> fixedV = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, system, ConoscopeFixedAxis.Vertical, 0, 30);

        Assert.Equal(new double[] { -60, -30, 0, 30, 60 }, fixedH.Select(item => item.PositionDegrees));
        Assert.All(fixedH, item => Assert.Equal(0, item.HorizontalAngle));
        Assert.All(fixedV, item => Assert.Equal(0, item.VerticalAngle));
        for (int index = 0; index < fixedH.Count; index++)
        {
            Assert.True(fixedH[index].IsValid);
            Assert.True(fixedV[index].IsValid);
            Assert.Equal(60, fixedH[index].SourceX, 8);
            Assert.Equal(120 - 30 * index, fixedH[index].SourceY, 8);
            Assert.Equal(30 * index, fixedV[index].SourceX, 8);
            Assert.Equal(60, fixedV[index].SourceY, 8);
        }
    }

    [Theory]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical, true)]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar, false)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar, false)]
    public void SamplesOutsideTheSelectedValidDomainRemainGaps(ConoscopeCoordinateSystem system, bool valid)
    {
        ConoscopeHorizontalVerticalSample sample = ConoscopeHorizontalVerticalCrossSection.Sample(
            Context(), system, ConoscopeFixedAxis.Horizontal, 50, 10).Single(item => item.PositionDegrees == 50);

        Assert.Equal(valid, sample.IsValid);
        if (!valid)
        {
            Assert.Equal(50, sample.HorizontalAngle);
            Assert.Equal(50, sample.VerticalAngle);
            AssertInvalid(sample);
        }
    }

    [Fact]
    public void ALineOutsideTheInstrumentFieldDoesNotReadSourcePixels()
    {
        int reads = 0;
        ConoscopeExportContext context = Context(read: (x, y) => { reads++; return new(x, y, 0); });
        IReadOnlyList<ConoscopeHorizontalVerticalSample> samples = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Horizontal, 70, 10);

        Assert.Equal(13, samples.Count);
        Assert.All(samples, AssertInvalid);
        Assert.Equal(0, reads);
    }

    [Theory]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical)]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar)]
    public void BilinearSamplingPreservesLinearFieldsAndSignedChannels(ConoscopeCoordinateSystem system)
    {
        ConoscopeExportContext context = Context(width: 301, height: 301, center: new Point(150.3, 149.7), scale: 1.35,
            read: (x, y) => LinearField(x, y));
        ConoscopeHorizontalVerticalSample sample = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, system, ConoscopeFixedAxis.Horizontal, 13, 20).Single(item => item.PositionDegrees == 20);
        ConoscopeXyzValue expected = LinearField(sample.SourceX, sample.SourceY);

        Assert.True(sample.IsValid);
        Assert.NotEqual(Math.Round(sample.SourceX), sample.SourceX);
        Assert.NotEqual(Math.Round(sample.SourceY), sample.SourceY);
        Assert.Equal(expected.X, sample.Xyz.X, 9);
        Assert.Equal(expected.Y, sample.Xyz.Y, 9);
        Assert.Equal(expected.Z, sample.Xyz.Z, 9);
        Assert.True(sample.Xyz.Z < 0);
    }

    [Theory]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical)]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar)]
    public void OriginalResolutionCoordinatesAreIndependentOfThePreviewCap(ConoscopeCoordinateSystem system)
    {
        ConoscopeExportContext context = Context(width: 9568, height: 6380, center: new Point(4784.25, 3190.5),
            scale: 6380 / 120.0, read: (x, y) => new(x, y, x + y));
        ConoscopeHorizontalVerticalSample sample = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, system, ConoscopeFixedAxis.Vertical, 0, 30).Single(item => item.PositionDegrees == 30);

        Assert.True(sample.IsValid);
        Assert.Equal(6379.25, sample.SourceX, 8);
        Assert.Equal(3190.5, sample.SourceY, 8);
        Assert.True(sample.SourceX > ConoscopeHorizontalVerticalProjection.DefaultMaximumPreviewSide);
        Assert.Equal(sample.SourceX, sample.Xyz.X, 8);
        Assert.Equal(sample.SourceY, sample.Xyz.Y, 8);
    }

    [Fact]
    public void RightAndBottomGeometricEndpointsAreNotClampedToTheLastPixel()
    {
        ConoscopeExportContext context = Context(width: 120, height: 120, read: (x, y) =>
        {
            Assert.InRange(x, 0, 119);
            Assert.InRange(y, 0, 119);
            return new(x, y, 1);
        });
        IReadOnlyList<ConoscopeHorizontalVerticalSample> fixedH = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Horizontal, 0, 60);
        IReadOnlyList<ConoscopeHorizontalVerticalSample> fixedV = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 60);

        AssertInvalid(fixedH[0]);
        AssertInvalid(fixedV[^1]);
        Assert.True(fixedH[^1].IsValid);
        Assert.True(fixedV[0].IsValid);
        Assert.True(fixedH[1].IsValid);
        Assert.True(fixedV[1].IsValid);
    }

    [Fact]
    public void LastValidPixelDoesNotRequireAnOutsideInterpolationNeighbour()
    {
        List<(int X, int Y)> reads = [];
        ConoscopeExportContext context = Context(width: 1, height: 1, center: new Point(0, 0), read: (x, y) =>
        {
            reads.Add((x, y));
            return new(2, 3, 4);
        });
        IReadOnlyList<ConoscopeHorizontalVerticalSample> samples = ConoscopeHorizontalVerticalCrossSection.Sample(
            context, ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Horizontal, 0, 60);

        Assert.Equal(new[] { (0, 0) }, reads);
        Assert.Equal(new ConoscopeXyzValue(2, 3, 4), samples[1].Xyz);
        Assert.True(samples[1].IsValid);
        AssertInvalid(samples[0]);
        AssertInvalid(samples[^1]);
    }

    [Fact]
    public void NonDivisibleStepKeepsBothExactEndpointsWithoutOvershooting()
    {
        IReadOnlyList<ConoscopeHorizontalVerticalSample> samples = ConoscopeHorizontalVerticalCrossSection.Sample(
            Context(), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 7);

        Assert.Equal(19, samples.Count);
        Assert.Equal(-60, samples[0].PositionDegrees);
        Assert.Equal(59, samples[^2].PositionDegrees);
        Assert.Equal(60, samples[^1].PositionDegrees);
        Assert.All(samples, sample => Assert.InRange(sample.PositionDegrees, -60, 60));
    }

    [Fact]
    public void DecimalStepDoesNotCreateANearDuplicateEndpoint()
    {
        IReadOnlyList<ConoscopeHorizontalVerticalSample> samples = ConoscopeHorizontalVerticalCrossSection.Sample(
            Context(maxAngle: 0.07), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 0.01);

        Assert.Equal(15, samples.Count);
        Assert.Equal(-0.07, samples[0].PositionDegrees);
        Assert.Equal(0.06, samples[^2].PositionDegrees, 12);
        Assert.Equal(0.07, samples[^1].PositionDegrees);
    }

    [Fact]
    public void AStepLargerThanTheFieldStillIncludesBothEndpoints()
    {
        IReadOnlyList<ConoscopeHorizontalVerticalSample> samples = ConoscopeHorizontalVerticalCrossSection.Sample(
            Context(), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 300);

        Assert.Equal(new double[] { -60, 60 }, samples.Select(item => item.PositionDegrees));
    }

    [Fact]
    public void MinimumStepBoundsTheNumberOfSamplesAndPreservesTheExactEndpoint()
    {
        IReadOnlyList<ConoscopeHorizontalVerticalSample> samples = ConoscopeHorizontalVerticalCrossSection.Sample(
            Context(maxAngle: 89.999), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 0.01);

        Assert.Equal(18001, samples.Count);
        Assert.Equal(-89.999, samples[0].PositionDegrees);
        Assert.Equal(89.999, samples[^1].PositionDegrees);
        Assert.True(samples.Zip(samples.Skip(1)).All(pair => pair.First.PositionDegrees < pair.Second.PositionDegrees));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.009)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidStepsAreRejectedBeforeSampling(double step)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(
            Context(), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, step));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(-60)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidPolarLimitsAreRejected(double maxAngle)
    {
        Assert.Throws<ArgumentException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(
            Context(maxAngle: maxAngle), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 1));
    }

    [Fact]
    public void InvalidGeometryModesAndFixedAnglesAreRejected()
    {
        ConoscopeExportContext context = Context();
        Assert.Throws<ArgumentNullException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(null!, ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 1));
        Assert.Throws<ArgumentException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(Context(width: 0), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 1));
        Assert.Throws<ArgumentException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(Context(height: 0), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 1));
        Assert.Throws<ArgumentException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(Context(scale: 0), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 1));
        Assert.Throws<ArgumentException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(Context(scale: double.NaN), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 1));
        Assert.Throws<ArgumentException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(Context(center: new Point(double.NaN, 0)), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 1));
        Assert.Throws<ArgumentException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(Context(center: new Point(0, double.PositiveInfinity)), ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(context, ConoscopeCoordinateSystem.Polar, ConoscopeFixedAxis.Vertical, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(context, ConoscopeCoordinateSystem.HorizontalVertical, (ConoscopeFixedAxis)100, 0, 1));
        foreach (double fixedAngle in new[] { double.NaN, double.PositiveInfinity, -90, 90 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ConoscopeHorizontalVerticalCrossSection.Sample(context, ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeFixedAxis.Vertical, fixedAngle, 1));
        }
    }

    private static ConoscopeExportContext Context(int width = 121, int height = 121, Point? center = null,
        double scale = 1, double maxAngle = 60, Func<int, int, ConoscopeXyzValue>? read = null)
    {
        return new()
        {
            ModelName = "Synthetic",
            ImageWidth = width,
            ImageHeight = height,
            Center = center ?? new Point(60, 60),
            PixelsPerDegree = scale,
            MaxAngle = maxAngle,
            ReadXyz = read ?? ((x, y) => new(x, y, 1))
        };
    }

    private static ConoscopeXyzValue LinearField(double x, double y) => new(2 * x - 3 * y + 5, 0.5 * x + 1.2 * y + 7, -x - 2 * y - 11);

    private static void AssertInvalid(ConoscopeHorizontalVerticalSample sample)
    {
        Assert.False(sample.IsValid);
        Assert.True(double.IsNaN(sample.SourceX));
        Assert.True(double.IsNaN(sample.SourceY));
        Assert.True(double.IsNaN(sample.Xyz.X));
        Assert.True(double.IsNaN(sample.Xyz.Y));
        Assert.True(double.IsNaN(sample.Xyz.Z));
    }
}
