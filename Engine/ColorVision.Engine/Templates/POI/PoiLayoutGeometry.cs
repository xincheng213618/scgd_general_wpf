using ColorVision.ImageEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.POI
{
    internal static class PoiLayoutGeometry
    {
        private const double Epsilon = 1e-9;

        public static bool TryNormalizeQuadrilateral(IReadOnlyList<Point> corners, out List<Point> result)
        {
            result = [];
            if (corners == null || corners.Count != 4)
                return false;
            if (corners.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
                return false;

            Point center = new(corners.Average(point => point.X), corners.Average(point => point.Y));
            List<Point> ordered = corners
                .OrderBy(point => Math.Atan2(point.Y - center.Y, point.X - center.X))
                .ToList();

            int topLeftIndex = Enumerable.Range(0, ordered.Count)
                .OrderBy(index => ordered[index].X + ordered[index].Y)
                .ThenBy(index => ordered[index].Y)
                .ThenBy(index => ordered[index].X)
                .First();
            result = Enumerable.Range(0, ordered.Count)
                .Select(offset => ordered[(topLeftIndex + offset) % ordered.Count])
                .ToList();

            double signedArea = SignedArea(result);
            if (!double.IsFinite(signedArea) || Math.Abs(signedArea) < Epsilon)
                return false;
            if (signedArea < 0)
            {
                result = [result[0], result[3], result[2], result[1]];
                signedArea = -signedArea;
            }

            return IsConvex(result, signedArea);
        }

        public static bool TryGetCollapsedPoint(IReadOnlyList<Point> corners, out Point point)
        {
            point = default;
            if (corners == null || corners.Count != 4)
                return false;
            if (corners.Any(corner => !double.IsFinite(corner.X) || !double.IsFinite(corner.Y)))
                return false;

            Point center = new(corners.Average(corner => corner.X), corners.Average(corner => corner.Y));
            if (!corners.All(corner => (corner - center).LengthSquared <= Epsilon * Epsilon))
                return false;

            point = center;
            return true;
        }

        public static List<Point> CreateQuadrilateralGrid(IReadOnlyList<Point> corners, int rows, int columns)
        {
            ArgumentNullException.ThrowIfNull(corners);
            ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
            if (corners.Count != 4)
                throw new ArgumentException("四边形必须包含四个角点。", nameof(corners));

            List<Point> points = new(rows * columns);

            for (int row = 0; row < rows; row++)
            {
                double v = rows == 1 ? 0.5 : (double)row / (rows - 1);
                for (int column = 0; column < columns; column++)
                {
                    double u = columns == 1 ? 0.5 : (double)column / (columns - 1);
                    points.Add(new Point(
                        (1 - v) * (1 - u) * corners[0].X +
                        (1 - v) * u * corners[1].X +
                        v * u * corners[2].X +
                        v * (1 - u) * corners[3].X,
                        (1 - v) * (1 - u) * corners[0].Y +
                        (1 - v) * u * corners[1].Y +
                        v * u * corners[2].Y +
                        v * (1 - u) * corners[3].Y));
                }
            }

            return points;
        }

        public static bool TryGetAutoFitSize(IReadOnlyList<Point> corners, int rows, int columns, GraphicTypes pointType, out Size size)
        {
            size = default;
            if (rows < 1 || columns < 1 || (pointType != GraphicTypes.Circle && pointType != GraphicTypes.Rect))
                return false;
            if (!TryNormalizeQuadrilateral(corners, out List<Point> points))
                return false;

            // Estimate one sampling window from the opposing edges and the grid counts.
            double width = ((points[1] - points[0]).Length + (points[2] - points[3]).Length) / 2 / columns;
            double height = ((points[3] - points[0]).Length + (points[2] - points[1]).Length) / 2 / rows;
            bool isCircle = pointType == GraphicTypes.Circle;
            if (isCircle)
                width = height = Math.Min(width, height);
            if (!double.IsFinite(width) || !double.IsFinite(height) || width > int.MaxValue || height > int.MaxValue)
                return false;

            bool Fits(double scale) => isCircle
                ? TryOffsetForCircle(points, width * scale / 2, DrawingGraphicPosition.Internal, out _)
                : TryOffsetForRectangle(points, width * scale, height * scale, DrawingGraphicPosition.Internal, out _);

            double scale = 1;
            if (!Fits(scale))
            {
                // Skewed areas and singleton grids may need smaller windows. Keep a nonzero
                // center-layout area so the existing Internal placement remains drawable.
                double lower = 0;
                double upper = 1;
                for (int iteration = 0; iteration < 48; iteration++)
                {
                    double middle = (lower + upper) / 2;
                    if (Fits(middle))
                        lower = middle;
                    else
                        upper = middle;
                }
                scale = lower;
            }

            width = isCircle ? 2 * Math.Floor(width * scale / 2) : Math.Floor(width * scale);
            height = isCircle ? width : Math.Floor(height * scale);
            if (width < 1 || height < 1 || !Fits(1))
                return false;

            size = new Size(width, height);
            return true;
        }

        public static bool TryOffsetForCircle(IReadOnlyList<Point> corners, double radius, DrawingGraphicPosition position, out List<Point> result)
        {
            return TryOffset(corners, position, _ => radius, out result);
        }

        public static bool TryOffsetForRectangle(IReadOnlyList<Point> corners, double width, double height, DrawingGraphicPosition position, out List<Point> result)
        {
            double halfWidth = width / 2;
            double halfHeight = height / 2;
            return TryOffset(corners, position, normal => Math.Abs(normal.X) * halfWidth + Math.Abs(normal.Y) * halfHeight, out result);
        }

        private static bool TryOffset(IReadOnlyList<Point> corners, DrawingGraphicPosition position, Func<Vector, double> getSupportDistance, out List<Point> result)
        {
            if (!TryNormalizeQuadrilateral(corners, out result))
                return false;

            corners = result;
            double originalArea = SignedArea(corners);
            if (position == DrawingGraphicPosition.LineOn)
                return true;

            double orientation = Math.Sign(originalArea);
            double offsetDirection = position == DrawingGraphicPosition.Internal ? 1 : -1;
            ShiftedEdge[] edges = new ShiftedEdge[corners.Count];

            for (int i = 0; i < corners.Count; i++)
            {
                Point start = corners[i];
                Vector direction = corners[(i + 1) % corners.Count] - start;
                double length = direction.Length;
                if (length < Epsilon)
                    return false;

                Vector inwardNormal = orientation > 0
                    ? new Vector(-direction.Y / length, direction.X / length)
                    : new Vector(direction.Y / length, -direction.X / length);
                double supportDistance = getSupportDistance(inwardNormal);
                if (!double.IsFinite(supportDistance) || supportDistance < 0)
                    return false;

                edges[i] = new ShiftedEdge(start + inwardNormal * (supportDistance * offsetDirection), direction);
            }

            List<Point> offsetCorners = new(corners.Count);
            for (int i = 0; i < corners.Count; i++)
            {
                if (!TryIntersect(edges[(i + corners.Count - 1) % corners.Count], edges[i], out Point intersection))
                    return false;
                offsetCorners.Add(intersection);
            }

            double offsetArea = SignedArea(offsetCorners);
            if (!double.IsFinite(offsetArea) || Math.Sign(offsetArea) != Math.Sign(originalArea) || Math.Abs(offsetArea) < Epsilon)
                return false;

            if (position == DrawingGraphicPosition.Internal)
            {
                if (Math.Abs(offsetArea) >= Math.Abs(originalArea) || !IsInsideShiftedEdges(edges, offsetCorners, orientation))
                    return false;
            }
            else if (Math.Abs(offsetArea) <= Math.Abs(originalArea))
            {
                return false;
            }

            result = offsetCorners;
            return true;
        }

        private static bool IsConvex(IReadOnlyList<Point> corners, double signedArea)
        {
            double orientation = Math.Sign(signedArea);
            for (int i = 0; i < corners.Count; i++)
            {
                Vector first = corners[(i + 1) % corners.Count] - corners[i];
                Vector second = corners[(i + 2) % corners.Count] - corners[(i + 1) % corners.Count];
                if (Cross(first, second) * orientation <= Epsilon)
                    return false;
            }
            return true;
        }

        private static bool IsInsideShiftedEdges(IReadOnlyList<ShiftedEdge> edges, IReadOnlyList<Point> points, double orientation)
        {
            foreach (Point point in points)
            {
                foreach (ShiftedEdge edge in edges)
                {
                    Vector toPoint = point - edge.Origin;
                    if (Cross(edge.Direction, toPoint) * orientation < -Epsilon)
                        return false;
                }
            }
            return true;
        }

        private static bool TryIntersect(ShiftedEdge first, ShiftedEdge second, out Point intersection)
        {
            double denominator = Cross(first.Direction, second.Direction);
            if (Math.Abs(denominator) < Epsilon)
            {
                intersection = default;
                return false;
            }

            double distance = Cross(second.Origin - first.Origin, second.Direction) / denominator;
            intersection = first.Origin + first.Direction * distance;
            return double.IsFinite(intersection.X) && double.IsFinite(intersection.Y);
        }

        private static double SignedArea(IReadOnlyList<Point> points)
        {
            double twiceArea = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Point current = points[i];
                Point next = points[(i + 1) % points.Count];
                twiceArea += current.X * next.Y - next.X * current.Y;
            }
            return twiceArea / 2;
        }

        private static double Cross(Vector first, Vector second) => first.X * second.Y - first.Y * second.X;

        private readonly record struct ShiftedEdge(Point Origin, Vector Direction);
    }
}
