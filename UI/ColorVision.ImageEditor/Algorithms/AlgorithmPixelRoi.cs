using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Shared pixel-center ROI projection and clipping used by local analysis providers.</summary>
    internal sealed class AlgorithmPixelRoi
    {
        private const double BoundaryTolerance = 1e-9;
        private AlgorithmPixelRoiKind Kind { get; init; }
        private double Left { get; init; }
        private double Top { get; init; }
        private double Right { get; init; }
        private double Bottom { get; init; }
        private double CenterX { get; init; }
        private double CenterY { get; init; }
        private double RadiusX { get; init; }
        private double RadiusY { get; init; }
        private IReadOnlyList<AlgorithmPoint> Points { get; init; } = Array.Empty<AlgorithmPoint>();

        public int MinimumX { get; private init; }
        public int MinimumY { get; private init; }
        public int MaximumXExclusive { get; private init; }
        public int MaximumYExclusive { get; private init; }
        public bool WasClipped { get; private init; }
        public bool IsEmpty => MinimumX >= MaximumXExclusive || MinimumY >= MaximumYExclusive;
        public required AlgorithmGeometry Geometry { get; init; }

        public static AlgorithmPixelRoi WholeImage(AlgorithmImageBuffer image) => new()
        {
            Kind = AlgorithmPixelRoiKind.Rectangle,
            Left = 0,
            Top = 0,
            Right = image.Width,
            Bottom = image.Height,
            MinimumX = 0,
            MinimumY = 0,
            MaximumXExclusive = image.Width,
            MaximumYExclusive = image.Height,
            Geometry = new AlgorithmGeometry("comparison-region", AlgorithmGeometryKind.Rectangle, [new(0, 0), new(image.Width, image.Height)]),
        };

        public static AlgorithmPixelRoi Create(AlgorithmRoi roi, AlgorithmImageBuffer image)
        {
            AlgorithmPixelRoi raw = roi switch
            {
                RectangleAlgorithmRoi rectangle => CreateRectangle(rectangle, image),
                CircleAlgorithmRoi circle => CreateCircle(circle, image),
                PolygonAlgorithmRoi polygon => CreatePolygon(polygon, image),
                _ => throw new ArgumentOutOfRangeException(nameof(roi)),
            };
            int minimumX = Math.Clamp(raw.MinimumX, 0, image.Width);
            int minimumY = Math.Clamp(raw.MinimumY, 0, image.Height);
            int maximumX = Math.Clamp(raw.MaximumXExclusive, 0, image.Width);
            int maximumY = Math.Clamp(raw.MaximumYExclusive, 0, image.Height);
            return new AlgorithmPixelRoi
            {
                Kind = raw.Kind,
                Left = raw.Left,
                Top = raw.Top,
                Right = raw.Right,
                Bottom = raw.Bottom,
                CenterX = raw.CenterX,
                CenterY = raw.CenterY,
                RadiusX = raw.RadiusX,
                RadiusY = raw.RadiusY,
                Points = raw.Points,
                MinimumX = minimumX,
                MinimumY = minimumY,
                MaximumXExclusive = maximumX,
                MaximumYExclusive = maximumY,
                WasClipped = minimumX != raw.MinimumX || minimumY != raw.MinimumY
                    || maximumX != raw.MaximumXExclusive || maximumY != raw.MaximumYExclusive,
                Geometry = raw.Geometry,
            };
        }

        public bool Contains(int x, int y) => Kind switch
        {
            AlgorithmPixelRoiKind.Rectangle => x >= Left && x < Right && y >= Top && y < Bottom,
            AlgorithmPixelRoiKind.Circle => Math.Pow((x - CenterX) / RadiusX, 2) + Math.Pow((y - CenterY) / RadiusY, 2) <= 1 + BoundaryTolerance,
            AlgorithmPixelRoiKind.Polygon => ContainsPolygon(x, y),
            _ => false,
        };

        private bool ContainsPolygon(double x, double y)
        {
            bool inside = false;
            for (int current = 0, previous = Points.Count - 1; current < Points.Count; previous = current++)
            {
                AlgorithmPoint a = Points[previous];
                AlgorithmPoint b = Points[current];
                if (OnSegment(x, y, a, b)) return true;
                bool crosses = (a.Y > y) != (b.Y > y)
                    && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static bool OnSegment(double x, double y, AlgorithmPoint a, AlgorithmPoint b)
        {
            double cross = (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);
            if (Math.Abs(cross) > BoundaryTolerance) return false;
            return x >= Math.Min(a.X, b.X) - BoundaryTolerance && x <= Math.Max(a.X, b.X) + BoundaryTolerance
                && y >= Math.Min(a.Y, b.Y) - BoundaryTolerance && y <= Math.Max(a.Y, b.Y) + BoundaryTolerance;
        }

        private static AlgorithmPixelRoi CreateRectangle(RectangleAlgorithmRoi roi, AlgorithmImageBuffer image)
        {
            AlgorithmPoint origin = AlgorithmCoordinates.ToPixel(new AlgorithmPoint(roi.X, roi.Y), roi.CoordinateSpace, image.DpiX, image.DpiY);
            AlgorithmPoint end = AlgorithmCoordinates.ToPixel(new AlgorithmPoint(roi.X + roi.Width, roi.Y + roi.Height), roi.CoordinateSpace, image.DpiX, image.DpiY);
            double left = Math.Min(origin.X, end.X);
            double top = Math.Min(origin.Y, end.Y);
            double right = Math.Max(origin.X, end.X);
            double bottom = Math.Max(origin.Y, end.Y);
            return new AlgorithmPixelRoi
            {
                Kind = AlgorithmPixelRoiKind.Rectangle,
                Left = left,
                Top = top,
                Right = right,
                Bottom = bottom,
                MinimumX = (int)Math.Floor(left),
                MinimumY = (int)Math.Floor(top),
                MaximumXExclusive = (int)Math.Ceiling(right),
                MaximumYExclusive = (int)Math.Ceiling(bottom),
                Geometry = new AlgorithmGeometry("roi", AlgorithmGeometryKind.Rectangle, [new(left, top), new(right, bottom)]),
            };
        }

        private static AlgorithmPixelRoi CreateCircle(CircleAlgorithmRoi roi, AlgorithmImageBuffer image)
        {
            AlgorithmPoint center = AlgorithmCoordinates.ToPixel(roi.Center, roi.CoordinateSpace, image.DpiX, image.DpiY);
            double radiusX = roi.CoordinateSpace == AlgorithmCoordinateSpace.Pixel ? roi.Radius : roi.Radius * image.DpiX / 25.4;
            double radiusY = roi.CoordinateSpace == AlgorithmCoordinateSpace.Pixel ? roi.Radius : roi.Radius * image.DpiY / 25.4;
            AlgorithmGeometry geometry;
            if (Math.Abs(radiusX - radiusY) <= 1e-9)
            {
                geometry = new AlgorithmGeometry("roi", AlgorithmGeometryKind.Circle, [center], Radius: radiusX);
            }
            else
            {
                AlgorithmPoint[] points = Enumerable.Range(0, 64).Select(index =>
                {
                    double angle = index * Math.PI * 2 / 64;
                    return new AlgorithmPoint(center.X + Math.Cos(angle) * radiusX, center.Y + Math.Sin(angle) * radiusY);
                }).ToArray();
                geometry = new AlgorithmGeometry("roi", AlgorithmGeometryKind.Polygon, points);
            }
            return new AlgorithmPixelRoi
            {
                Kind = AlgorithmPixelRoiKind.Circle,
                CenterX = center.X,
                CenterY = center.Y,
                RadiusX = radiusX,
                RadiusY = radiusY,
                MinimumX = (int)Math.Floor(center.X - radiusX),
                MinimumY = (int)Math.Floor(center.Y - radiusY),
                MaximumXExclusive = checked((int)Math.Floor(center.X + radiusX) + 1),
                MaximumYExclusive = checked((int)Math.Floor(center.Y + radiusY) + 1),
                Geometry = geometry,
            };
        }

        private static AlgorithmPixelRoi CreatePolygon(PolygonAlgorithmRoi roi, AlgorithmImageBuffer image)
        {
            AlgorithmPoint[] points = roi.Points
                .Select(point => AlgorithmCoordinates.ToPixel(point, roi.CoordinateSpace, image.DpiX, image.DpiY))
                .ToArray();
            double minimumX = points.Min(point => point.X);
            double minimumY = points.Min(point => point.Y);
            double maximumX = points.Max(point => point.X);
            double maximumY = points.Max(point => point.Y);
            return new AlgorithmPixelRoi
            {
                Kind = AlgorithmPixelRoiKind.Polygon,
                Points = points,
                MinimumX = (int)Math.Floor(minimumX),
                MinimumY = (int)Math.Floor(minimumY),
                MaximumXExclusive = checked((int)Math.Floor(maximumX) + 1),
                MaximumYExclusive = checked((int)Math.Floor(maximumY) + 1),
                Geometry = new AlgorithmGeometry("roi", AlgorithmGeometryKind.Polygon, points),
            };
        }

        private enum AlgorithmPixelRoiKind { Rectangle, Circle, Polygon }
    }
}
