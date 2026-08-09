using Conoscope.Analysis;
using Conoscope.Core;
using System;
using System.Windows;

namespace Conoscope.ApplicationServices.Analysis
{
    public static class FocusPointMeasurementService
    {
        public static unsafe bool TryCalculateCircleRoiAverage(
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

                float* xRow = (float*)xMat.Ptr(iy);
                float* yRow = (float*)yMat.Ptr(iy);
                float* zRow = (float*)zMat.Ptr(iy);
                for (int ix = startX; ix <= endX; ix++)
                {
                    double dx = radiusX <= 0 ? 0 : (ix - centerX) / radiusX;
                    if (dx * dx + dy2 > 1)
                    {
                        continue;
                    }

                    float xValue = xRow[ix];
                    float yValue = yRow[ix];
                    float zValue = zRow[ix];
                    if (!float.IsFinite(xValue) || !float.IsFinite(yValue) || !float.IsFinite(zValue))
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

        public static double GetFullAzimuthAngle(Point point, Point imageCenter)
        {
            double deltaX = point.X - imageCenter.X;
            double deltaY = imageCenter.Y - point.Y;
            return NormalizeFullAzimuthAngle(Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI);
        }

        public static double NormalizeFullAzimuthAngle(double angleDegrees)
        {
            double normalized = angleDegrees % 360.0;
            return normalized < 0 ? normalized + 360.0 : normalized;
        }

        public static Point CreatePointFromPolar(double azimuthDegrees, double distancePixels, Point imageCenter)
        {
            double radians = NormalizeFullAzimuthAngle(azimuthDegrees) * Math.PI / 180.0;
            double distance = Math.Max(0, distancePixels);
            return new Point(
                imageCenter.X + Math.Cos(radians) * distance,
                imageCenter.Y - Math.Sin(radians) * distance);
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

    }
}
