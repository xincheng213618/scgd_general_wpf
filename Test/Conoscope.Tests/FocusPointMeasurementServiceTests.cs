using Conoscope.Analysis;
using Conoscope.ApplicationServices.Analysis;
using Conoscope.Core;
using System.Windows;

namespace Conoscope.Tests
{
    public class FocusPointMeasurementServiceTests
    {
        [Fact]
        public void ResolveFocusCircleName_ReturnsProvidedName_WhenNotEmpty()
        {
            string result = FocusPointMeasurementService.ResolveFocusCircleName("MyCircle", 42);
            Assert.Equal("MyCircle", result);
        }

        [Fact]
        public void ResolveFocusCircleName_ReturnsFallback_WhenEmpty()
        {
            string result = FocusPointMeasurementService.ResolveFocusCircleName("", 7);
            Assert.Equal("Focus_7", result);
        }

        [Fact]
        public void ResolveFocusCircleName_ReturnsFallback_WhenWhitespace()
        {
            string result = FocusPointMeasurementService.ResolveFocusCircleName("   ", 3);
            Assert.Equal("Focus_3", result);
        }

        [Fact]
        public void GetFullAzimuthAngle_ReturnsZero_ForPointRightOfCenter()
        {
            Point center = new Point(100, 100);
            Point right = new Point(200, 100);
            double angle = FocusPointMeasurementService.GetFullAzimuthAngle(right, center);
            Assert.Equal(0, angle, 2);
        }

        [Fact]
        public void GetFullAzimuthAngle_Returns90_ForPointAboveCenter()
        {
            Point center = new Point(100, 100);
            Point above = new Point(100, 0);
            double angle = FocusPointMeasurementService.GetFullAzimuthAngle(above, center);
            Assert.Equal(90, angle, 2);
        }

        [Fact]
        public void GetFullAzimuthAngle_Returns270_ForPointBelowCenter()
        {
            Point center = new Point(100, 100);
            Point below = new Point(100, 200);
            double angle = FocusPointMeasurementService.GetFullAzimuthAngle(below, center);
            Assert.Equal(270, angle, 2);
        }

        [Fact]
        public void GetFullAzimuthAngle_Returns180_ForPointLeftOfCenter()
        {
            Point center = new Point(100, 100);
            Point left = new Point(0, 100);
            double angle = FocusPointMeasurementService.GetFullAzimuthAngle(left, center);
            Assert.Equal(180, angle, 2);
        }

        [Fact]
        public void GetPolarRadiusAngle_ReturnsZero_AtCenter()
        {
            Point center = new Point(100, 100);
            double angle = FocusPointMeasurementService.GetPolarRadiusAngle(center, center, 200, 90);
            Assert.Equal(0, angle, 6);
        }

        [Fact]
        public void GetPolarRadiusAngle_ReturnsMaxAngle_AtEdge()
        {
            Point center = new Point(100, 100);
            Point edge = new Point(300, 100);
            double angle = FocusPointMeasurementService.GetPolarRadiusAngle(edge, center, 200, 90);
            Assert.Equal(90, angle, 6);
        }

        [Fact]
        public void GetPolarRadiusAngle_ClampsToMaxAngle()
        {
            Point center = new Point(100, 100);
            Point far = new Point(500, 100);
            double angle = FocusPointMeasurementService.GetPolarRadiusAngle(far, center, 200, 90);
            Assert.Equal(90, angle, 6);
        }

        [Fact]
        public void GetPolarRadiusAngle_ReturnsZero_WhenImageRadiusIsZero()
        {
            Point center = new Point(100, 100);
            Point off = new Point(200, 100);
            double angle = FocusPointMeasurementService.GetPolarRadiusAngle(off, center, 0, 90);
            Assert.Equal(0, angle);
        }

        [Fact]
        public void GetFocusCircleRadiusAngle_UsesPixelsPerDegree()
        {
            double angle = FocusPointMeasurementService.GetFocusCircleRadiusAngle(50, 10, 200, 90);
            Assert.Equal(5, angle, 6);
        }

        [Fact]
        public void GetFocusCircleRadiusAngle_FallsBackToImageRadius()
        {
            double angle = FocusPointMeasurementService.GetFocusCircleRadiusAngle(100, 0, 200, 90);
            Assert.Equal(45, angle, 6);
        }

        [Fact]
        public void GetFocusCircleRadiusAngle_ReturnsZero_WhenBothZero()
        {
            double angle = FocusPointMeasurementService.GetFocusCircleRadiusAngle(50, 0, 0, 90);
            Assert.Equal(0, angle);
        }

        [Fact]
        public void GetPolarDistancePixels_UsesPixelsPerDegree()
        {
            double pixels = FocusPointMeasurementService.GetPolarDistancePixels(30, 10, 200, 90);
            Assert.Equal(300, pixels, 6);
        }

        [Fact]
        public void GetPolarDistancePixels_ClampsToMaxAngle()
        {
            double pixels = FocusPointMeasurementService.GetPolarDistancePixels(100, 10, 200, 90);
            Assert.Equal(900, pixels, 6);
        }

        [Fact]
        public void GetPolarAngleFromDistancePixels_UsesPixelsPerDegree()
        {
            double angle = FocusPointMeasurementService.GetPolarAngleFromDistancePixels(300, 10, 200, 90);
            Assert.Equal(30, angle, 6);
        }

        [Fact]
        public void GetPolarAngleFromDistancePixels_ClampsToMaxAngle()
        {
            double angle = FocusPointMeasurementService.GetPolarAngleFromDistancePixels(2000, 10, 200, 90);
            Assert.Equal(90, angle, 6);
        }

        [Fact]
        public void GetFocusCircleRadiusPixelsFromAngle_UsesPixelsPerDegree()
        {
            double pixels = FocusPointMeasurementService.GetFocusCircleRadiusPixelsFromAngle(5, 10, 200, 90, 1);
            Assert.Equal(50, pixels, 6);
        }

        [Fact]
        public void GetFocusCircleRadiusPixelsFromAngle_EnforcesMinimum()
        {
            double pixels = FocusPointMeasurementService.GetFocusCircleRadiusPixelsFromAngle(0.01, 10, 200, 90, 5);
            Assert.Equal(5, pixels, 6);
        }

        [Fact]
        public void CreatePointFromPolar_ReturnsCorrectPoint()
        {
            Point center = new Point(100, 100);
            Point result = FocusPointMeasurementService.CreatePointFromPolar(0, 50, center);
            Assert.Equal(150, result.X, 2);
            Assert.Equal(100, result.Y, 2);
        }

        [Fact]
        public void CreatePointFromPolar_90Degrees_GoesUp()
        {
            Point center = new Point(100, 100);
            Point result = FocusPointMeasurementService.CreatePointFromPolar(90, 50, center);
            Assert.Equal(100, result.X, 2);
            Assert.Equal(50, result.Y, 2);
        }

        [Fact]
        public void TryCalculateCircleRoiAverage_ReturnsFalse_WhenMatIsNull()
        {
            bool result = FocusPointMeasurementService.TryCalculateCircleRoiAverage(
                null, null, null, 100, 100, new Point(50, 50), 10,
                out _, out _, out _, out _);
            Assert.False(result);
        }

        [Fact]
        public void TryCalculateCircleRoiAverage_ReturnsFalse_WhenRadiusIsZero()
        {
            var mat = new OpenCvSharp.Mat(10, 10, OpenCvSharp.MatType.CV_32FC1, new OpenCvSharp.Scalar(1.0));
            bool result = FocusPointMeasurementService.TryCalculateCircleRoiAverage(
                mat, mat, mat, 10, 10, new Point(5, 5), 0,
                out _, out _, out _, out _);
            Assert.False(result);
            mat.Dispose();
        }
    }
}
