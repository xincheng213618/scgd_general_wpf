using Conoscope.ApplicationServices.Analysis;
using System.Windows;

namespace Conoscope.Tests;

public class FocusPointMathTests
{
    [Theory]
    [InlineData(200, 100, 0)]
    [InlineData(100, 0, 90)]
    [InlineData(0, 100, 180)]
    [InlineData(100, 200, 270)]
    public void FullAzimuthCoversEntireCircle(double x, double y, double expectedDegrees)
    {
        Point center = new(100, 100);

        double actual = FocusPointMeasurementService.GetFullAzimuthAngle(new Point(x, y), center);

        Assert.Equal(expectedDegrees, actual, precision: 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(180)]
    [InlineData(225)]
    [InlineData(270)]
    [InlineData(359)]
    public void PolarPointRoundTripsThroughFullAzimuth(double azimuthDegrees)
    {
        Point center = new(320, 240);
        Point point = FocusPointMeasurementService.CreatePointFromPolar(azimuthDegrees, 120, center);

        double actual = FocusPointMeasurementService.GetFullAzimuthAngle(point, center);

        Assert.Equal(azimuthDegrees, actual, precision: 6);
    }

    [Fact]
    public void CircleRoiAverageUsesOnlyFiniteTriplesInsideCircle()
    {
        using OpenCvSharp.Mat x = new(5, 5, OpenCvSharp.MatType.CV_32FC1, OpenCvSharp.Scalar.All(2));
        using OpenCvSharp.Mat y = new(5, 5, OpenCvSharp.MatType.CV_32FC1, OpenCvSharp.Scalar.All(4));
        using OpenCvSharp.Mat z = new(5, 5, OpenCvSharp.MatType.CV_32FC1, OpenCvSharp.Scalar.All(6));
        x.Set(2, 2, float.NaN);

        bool success = FocusPointMeasurementService.TryCalculateCircleRoiAverage(
            x, y, z, 5, 5, new Point(2, 2), 1.5,
            out double averageX, out double averageY, out double averageZ, out int count);

        Assert.True(success);
        Assert.Equal(8, count);
        Assert.Equal(2, averageX, precision: 6);
        Assert.Equal(4, averageY, precision: 6);
        Assert.Equal(6, averageZ, precision: 6);
    }
}
