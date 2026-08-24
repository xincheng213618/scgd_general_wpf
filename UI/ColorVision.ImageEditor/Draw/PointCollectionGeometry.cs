using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.Draw
{
    internal static class PointCollectionGeometry
    {
        internal static Rect GetBounds(List<Point>? points)
        {
            if (points == null || points.Count == 0)
                return Rect.Empty;

            Point firstPoint = points[0];
            double minX = firstPoint.X;
            double minY = firstPoint.Y;
            double maxX = firstPoint.X;
            double maxY = firstPoint.Y;
            for (int i = 1; i < points.Count; i++)
            {
                Point point = points[i];
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }

        internal static bool MapToRect(List<Point>? points, Rect target)
        {
            Rect source = GetBounds(points);
            if (source.IsEmpty || points == null)
                return false;

            bool hasWidth = source.Width > 0;
            bool hasHeight = source.Height > 0;
            double scaleX = hasWidth ? target.Width / source.Width : 0;
            double scaleY = hasHeight ? target.Height / source.Height : 0;
            double targetRight = target.X + target.Width;
            double targetBottom = target.Y + target.Height;

            for (int i = 0; i < points.Count; i++)
            {
                double x = hasWidth
                    ? (points[i].X - source.X) * scaleX + target.X
                    : target.X + target.Width / 2;
                double y = hasHeight
                    ? (points[i].Y - source.Y) * scaleY + target.Y
                    : target.Y + target.Height / 2;

                x = Math.Max(target.X, Math.Min(x, targetRight));
                y = Math.Max(target.Y, Math.Min(y, targetBottom));
                points[i] = new Point(x, y);
            }

            return true;
        }
    }
}
