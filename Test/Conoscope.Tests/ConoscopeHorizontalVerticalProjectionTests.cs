using Conoscope.Core;
using OpenCvSharp;
using Point = System.Windows.Point;

namespace Conoscope.Tests;

public sealed class ConoscopeHorizontalVerticalProjectionTests
{
    [Theory]
    [InlineData(30, 0, 30, 0)]
    [InlineData(0, 30, 30, 90)]
    [InlineData(-30, 0, 30, 180)]
    [InlineData(0, -30, 30, 270)]
    [InlineData(45, 45, 54.735610317, 45)]
    public void ConvertsHorizontalVerticalAnglesToPolarAngles(double horizontal, double vertical, double expectedPolar, double expectedAzimuth)
    {
        Assert.True(ConoscopeHorizontalVerticalProjection.TryConvertHorizontalVerticalToPolar(
            ConoscopeCoordinateSystem.HorizontalVertical,
            horizontal,
            vertical,
            60,
            out double polar,
            out double azimuth));

        Assert.Equal(expectedPolar, polar, 6);
        Assert.Equal(expectedAzimuth, azimuth, 6);
    }

    [Fact]
    public void RejectsRoundedSquareCornersOutsideThePolarField()
    {
        Assert.False(ConoscopeHorizontalVerticalProjection.TryConvertHorizontalVerticalToPolar(
            ConoscopeCoordinateSystem.HorizontalVertical,
            55,
            55,
            60,
            out double polar,
            out _));
        Assert.True(polar > 60);
    }

    [Theory]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical, 0, 0)]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical, 30, 25)]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar, 59, 135)]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar, 40, 275)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar, 59, 135)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar, 40, 275)]
    public void PolarAndHorizontalVerticalConversionsRoundTrip(ConoscopeCoordinateSystem coordinateSystem, double polar, double azimuth)
    {
        Assert.True(ConoscopeHorizontalVerticalProjection.TryConvertPolarToHorizontalVertical(
            coordinateSystem,
            polar,
            azimuth,
            60,
            out double horizontal,
            out double vertical));
        Assert.True(ConoscopeHorizontalVerticalProjection.TryConvertHorizontalVerticalToPolar(
            coordinateSystem,
            horizontal,
            vertical,
            60,
            out double actualPolar,
            out double actualAzimuth));

        Assert.Equal(polar, actualPolar, 6);
        if (polar > 0)
        {
            Assert.Equal(azimuth, actualAzimuth, 6);
        }
    }

    [Theory]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar, 50.768479516, 37.761243907)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar, 37.761243907, 50.768479516)]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical, 50.768479516, 50.768479516)]
    public void ProjectedCoordinatesFollowTheManualFormulas(ConoscopeCoordinateSystem coordinateSystem, double expectedHorizontal, double expectedVertical)
    {
        Assert.True(ConoscopeHorizontalVerticalProjection.TryConvertPolarToHorizontalVertical(
            coordinateSystem,
            60,
            45,
            60,
            out double horizontal,
            out double vertical));

        Assert.Equal(expectedHorizontal, horizontal, 6);
        Assert.Equal(expectedVertical, vertical, 6);
    }

    [Theory]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical, true)]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar, false)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar, false)]
    public void CoordinateSystemsHaveTheirOwnValidDomain(ConoscopeCoordinateSystem coordinateSystem, bool expectedValid)
    {
        bool valid = ConoscopeHorizontalVerticalProjection.TryConvertHorizontalVerticalToPolar(
            coordinateSystem,
            50,
            50,
            60,
            out _,
            out _);

        Assert.Equal(expectedValid, valid);
    }

    [Fact]
    public void PolarIsNotAcceptedAsAProjectedCoordinateSystem()
    {
        Assert.False(ConoscopeHorizontalVerticalProjection.TryConvertPolarToHorizontalVertical(
            ConoscopeCoordinateSystem.Polar,
            30,
            45,
            60,
            out _,
            out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConoscopeHorizontalVerticalProjection.Create(
            101,
            101,
            new Point(50, 50),
            50 / 60.0,
            60,
            ConoscopeCoordinateSystem.Polar,
            maximumPreviewSide: 101));
    }

    [Fact]
    public void ProjectionCapsPreviewAndMapsCenterBackToSourceCenter()
    {
        using ConoscopeHorizontalVerticalProjection projection = ConoscopeHorizontalVerticalProjection.Create(
            9568,
            6380,
            new Point(4784, 3190),
            6380 / 120.0,
            60,
            ConoscopeCoordinateSystem.NorthPolar);

        Assert.Equal(ConoscopeHorizontalVerticalProjection.DefaultMaximumPreviewSide, projection.OutputSize);
        Assert.Equal(ConoscopeCoordinateSystem.NorthPolar, projection.CoordinateSystem);
        Assert.True(projection.TryMapDisplayPointToSource(
            projection.OutputCenter,
            out Point sourcePoint,
            out double horizontal,
            out double vertical,
            out double polar,
            out _));
        Assert.Equal(projection.SourceCenter.X, sourcePoint.X, 6);
        Assert.Equal(projection.SourceCenter.Y, sourcePoint.Y, 6);
        Assert.Equal(0, horizontal, 6);
        Assert.Equal(0, vertical, 6);
        Assert.Equal(0, polar, 6);
    }

    [Theory]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical)]
    [InlineData(ConoscopeCoordinateSystem.NorthPolar)]
    [InlineData(ConoscopeCoordinateSystem.EastPolar)]
    public void RemapProducesSquareGrayPreviewAndBlackInvalidCorners(ConoscopeCoordinateSystem coordinateSystem)
    {
        using Mat source = new(101, 101, MatType.CV_8UC1, Scalar.All(200));
        using ConoscopeHorizontalVerticalProjection projection = ConoscopeHorizontalVerticalProjection.Create(
            101,
            101,
            new Point(50, 50),
            50 / 60.0,
            60,
            coordinateSystem,
            maximumPreviewSide: 101);
        using Mat preview = projection.RemapGray8(source);

        Assert.Equal(101, preview.Rows);
        Assert.Equal(101, preview.Cols);
        Assert.Equal(200, preview.At<byte>(50, 50));
        Assert.Equal(0, preview.At<byte>(0, 0));
        Assert.Equal(200, preview.At<byte>(50, 100));
    }
}
