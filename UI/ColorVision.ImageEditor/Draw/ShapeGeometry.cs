using System;
using System.Windows;

namespace ColorVision.ImageEditor.Draw
{
    internal static class ShapeGeometry
    {
        internal static bool IsFinite(Point point)
        {
            return double.IsFinite(point.X) && double.IsFinite(point.Y);
        }

        internal static bool IsFinite(Rect rect)
        {
            return !rect.IsEmpty
                && double.IsFinite(rect.X)
                && double.IsFinite(rect.Y)
                && double.IsFinite(rect.Width)
                && double.IsFinite(rect.Height)
                && double.IsFinite(rect.Right)
                && double.IsFinite(rect.Bottom);
        }

        internal static bool TryGetEllipseBounds(Point center, double radiusX, double radiusY, out Rect bounds)
        {
            radiusX = Math.Abs(radiusX);
            radiusY = Math.Abs(radiusY);
            double left = center.X - radiusX;
            double top = center.Y - radiusY;
            double width = radiusX * 2;
            double height = radiusY * 2;
            if (!IsFinite(center)
                || !double.IsFinite(radiusX)
                || !double.IsFinite(radiusY)
                || !double.IsFinite(left)
                || !double.IsFinite(top)
                || !double.IsFinite(width)
                || !double.IsFinite(height))
            {
                bounds = Rect.Empty;
                return false;
            }

            Rect candidate = new(left, top, width, height);
            if (!IsFinite(candidate))
            {
                bounds = Rect.Empty;
                return false;
            }

            bounds = candidate;
            return true;
        }
    }
}
