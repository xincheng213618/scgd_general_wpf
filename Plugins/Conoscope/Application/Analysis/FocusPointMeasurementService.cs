using Conoscope.Analysis;
using Conoscope.Core;
using System;
using System.Windows;

namespace Conoscope.ApplicationServices.Analysis
{
    public static class FocusPointMeasurementService
    {
        public static bool TryCalculateCircleRoiAverage(
            OpenCvSharp.Mat xMat, OpenCvSharp.Mat yMat, OpenCvSharp.Mat zMat,
            int displayWidth, int displayHeight,
            Point imageCenter, double imageRadius,
            out double avgX, out double avgY, out double avgZ, out int sampleCount)
        {
            avgX = 0;
            avgY = 0;
            avgZ = 0;
            sampleCount = 0;

            if (xMat == null || yMat == null || zMat == null || imageRadius <= 0)
            {
                return false;
            }

            int xyzWidth = xMat.Width;
            int xyzHeight = xMat.Height;
            if (xyzWidth <= 0 || xyzHeight <= 0 || displayWidth <= 0 || displayHeight <= 0)
            {
                return false;
            }

            double scaleX = (double)xyzWidth / displayWidth;
            double scaleY = (double)xyzHeight / displayHeight;
            double centerX = imageCenter.X * scaleX;
            double centerY = imageCenter.Y * scaleY;
            double radiusX = Math.Max(imageRadius * scaleX, 0.5);
            double radiusY = Math.Max(imageRadius * scaleY, 0.5);

            int startX = Math.Max(0, (int)Math.Floor(centerX - radiusX));
            int endX = Math.Min(xyzWidth - 1, (int)Math.Ceiling(centerX + radiusX));
            int startY = Math.Max(0, (int)Math.Floor(centerY - radiusY));
            int endY = Math.Min(xyzHeight - 1, (int)Math.Ceiling(centerY + radiusY));

            double sumX = 0;
            double sumY = 0;
            double sumZ = 0;

            for (int iy = startY; iy <= endY; iy++)
            {
                double dy = radiusY <= 0 ? 0 : (iy - centerY) / radiusY;
                double dy2 = dy * dy;
                if (dy2 > 1)
                {
                    continue;
                }

                for (int ix = startX; ix <= endX; ix++)
                {
                    double dx = radiusX <= 0 ? 0 : (ix - centerX) / radiusX;
                    if (dx * dx + dy2 > 1)
                    {
                        continue;
                    }

                    double xValue = xMat.At<float>(iy, ix);
                    double yValue = yMat.At<float>(iy, ix);
                    double zValue = zMat.At<float>(iy, ix);
                    if (!double.IsFinite(xValue) || !double.IsFinite(yValue) || !double.IsFinite(zValue))
                    {
                        continue;
                    }

                    sumX += xValue;
                    sumY += yValue;
                    sumZ += zValue;
                    sampleCount++;
                }
            }

            if (sampleCount <= 0)
            {
                return false;
            }

            avgX = sumX / sampleCount;
            avgY = sumY / sampleCount;
            avgZ = sumZ / sampleCount;
            return true;
        }

        public static string ResolveFocusCircleName(string circleText, int circleId)
        {
            return string.IsNullOrWhiteSpace(circleText) ? $"Focus_{circleId}" : circleText;
        }

        public static double GetFocusCircleRadiusAngle(double radiusPixels, double pixelsPerDegree, double imageRadius, double maxAngle)
        {
            if (pixelsPerDegree > double.Epsilon)
            {
                return Math.Max(0, radiusPixels / pixelsPerDegree);
            }

            if (imageRadius > 0)
            {
                return Math.Max(0, Math.Min(radiusPixels / imageRadius * maxAngle, maxAngle));
            }

            return 0;
        }

        public static double GetPolarDistancePixels(double polarDegrees, double pixelsPerDegree, double imageRadius, double maxAngle)
        {
            double clampedPolar = Math.Max(0, Math.Min(polarDegrees, maxAngle));
            if (pixelsPerDegree > double.Epsilon)
            {
                return clampedPolar * pixelsPerDegree;
            }

            if (imageRadius > 0 && maxAngle > double.Epsilon)
            {
                return clampedPolar / maxAngle * imageRadius;
            }

            return 0;
        }

        public static double GetPolarAngleFromDistancePixels(double distancePixels, double pixelsPerDegree, double imageRadius, double maxAngle)
        {
            double distance = Math.Max(0, distancePixels);
            if (pixelsPerDegree > double.Epsilon)
            {
                return Math.Max(0, Math.Min(distance / pixelsPerDegree, maxAngle));
            }

            if (imageRadius > 0)
            {
                return Math.Max(0, Math.Min(distance / imageRadius * maxAngle, maxAngle));
            }

            return 0;
        }

        public static double GetFocusCircleRadiusPixelsFromAngle(double radiusDegrees, double pixelsPerDegree, double imageRadius, double maxAngle, double minimumRadius)
        {
            double angle = Math.Max(0, radiusDegrees);
            if (pixelsPerDegree > double.Epsilon)
            {
                return Math.Max(minimumRadius, angle * pixelsPerDegree);
            }

            if (imageRadius > 0 && maxAngle > double.Epsilon)
            {
                return Math.Max(minimumRadius, angle / maxAngle * imageRadius);
            }

            return minimumRadius;
        }

        public static double GetFullAzimuthAngle(Point point, Point imageCenter)
        {
            double deltaX = point.X - imageCenter.X;
            double deltaY = imageCenter.Y - point.Y;
            double angle = Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI;
            return angle < 0 ? angle + 360.0 : angle;
        }

        public static double GetPolarRadiusAngle(Point point, Point imageCenter, double imageRadius, double maxAngle)
        {
            if (imageRadius <= 0)
            {
                return 0;
            }

            double distance = (point - imageCenter).Length;
            return Math.Max(0, Math.Min(distance / imageRadius * maxAngle, maxAngle));
        }

        public static Point CreatePointFromPolar(double azimuthDegrees, double distancePixels, Point imageCenter)
        {
            double radians = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(azimuthDegrees) * Math.PI / 180.0;
            return new Point(
                imageCenter.X + Math.Cos(radians) * distancePixels,
                imageCenter.Y - Math.Sin(radians) * distancePixels);
        }
    }
}
