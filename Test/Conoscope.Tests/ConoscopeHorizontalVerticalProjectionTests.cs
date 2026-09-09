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
            55,
            55,
            60,
            out double polar,
            out _));
        Assert.True(polar > 60);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(30, 25)]
    [InlineData(59, 135)]
    [InlineData(40, 275)]
    public void PolarAndHorizontalVerticalConversionsRoundTrip(double polar, double azimuth)
    {
        Assert.True(ConoscopeHorizontalVerticalProjection.TryConvertPolarToHorizontalVertical(
            polar,
            azimuth,
            60,
            out double horizontal,
            out double vertical));
        Assert.True(ConoscopeHorizontalVerticalProjection.TryConvertHorizontalVerticalToPolar(
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

    [Fact]
    public void ProjectionCapsPreviewAndMapsCenterBackToSourceCenter()
    {
        using ConoscopeHorizontalVerticalProjection projection = ConoscopeHorizontalVerticalProjection.Create(
            9568,
            6380,
            new Point(4784, 3190),
            6380 / 120.0,
            60);

        Assert.Equal(ConoscopeHorizontalVerticalProjection.DefaultMaximumPreviewSide, projection.OutputSize);
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

    [Fact]
    public void RemapProducesSquareGrayPreviewAndBlackInvalidCorners()
    {
        using Mat source = new(101, 101, MatType.CV_8UC1, Scalar.All(200));
        using ConoscopeHorizontalVerticalProjection projection = ConoscopeHorizontalVerticalProjection.Create(
            101,
            101,
            new Point(50, 50),
            50 / 60.0,
            60,
            maximumPreviewSide: 101);
        using Mat preview = projection.RemapGray8(source);

        Assert.Equal(101, preview.Rows);
        Assert.Equal(101, preview.Cols);
        Assert.Equal(200, preview.At<byte>(50, 50));
        Assert.Equal(0, preview.At<byte>(0, 0));
        Assert.Equal(200, preview.At<byte>(50, 100));
    }
}
