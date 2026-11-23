using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.ImageEditor.Draw;

/// <summary>Image-coordinate geometry, independent of display zoom, stroke and fill.</summary>
public sealed class ClosedPixelRegion
{
    private readonly Point[]? vertices;
    private Rect ellipseBounds;
    private double rotation;
    private ClosedPixelRegion(Rect bounds, Point[]? vertices)
    {
        Bounds = bounds;
        this.vertices = vertices;
    }

    public Rect Bounds { get; }
    public bool IsEllipse => vertices == null;

    public static ClosedPixelRegion Ellipse(Point center, double radiusX, double radiusY, double rotation = 0)
    {
        if (!ShapeGeometry.TryGetEllipseBounds(center, radiusX, radiusY, out Rect bounds)
            || radiusX <= 0 || radiusY <= 0)
            throw new ArgumentException("椭圆的中心和半轴必须有效，半轴必须大于零。");
        if (!double.IsFinite(rotation)) throw new ArgumentException("旋转角度必须是有限数。");
        double angle = rotation * Math.PI / 180, c = Math.Cos(angle), s = Math.Sin(angle);
        double extentX = Math.Sqrt(radiusX * radiusX * c * c + radiusY * radiusY * s * s);
        double extentY = Math.Sqrt(radiusX * radiusX * s * s + radiusY * radiusY * c * c);
        return new(new Rect(center.X - extentX, center.Y - extentY, extentX * 2, extentY * 2), null) { ellipseBounds = bounds, rotation = rotation };
    }

    public static ClosedPixelRegion Polygon(IEnumerable<Point> points, double rotation = 0)
    {
        Point[] vertices = points.ToArray();
        if (vertices.Length < 3 || vertices.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            throw new ArgumentException("封闭区域需要至少三个有效顶点。");
        Rect bounds = PointCollectionGeometry.GetBounds(vertices.ToList());
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("封闭区域没有有效面积。");
        if (!double.IsFinite(rotation)) throw new ArgumentException("旋转角度必须是有限数。");
        if (rotation != 0)
        {
            var transform = new System.Windows.Media.RotateTransform(rotation, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            vertices = vertices.Select(p => transform.Transform(p)).ToArray();
            bounds = PointCollectionGeometry.GetBounds(vertices.ToList());
        }
        return new(bounds, vertices);
    }

    /// <summary>Integer pixel coordinates. Ellipses exclude their boundary, like legacy circles.
    /// Polygons use even-odd fill and half-open edges; returned runs never overlap.</summary>
    public IEnumerable<PixelRowRun> GetRuns(int width, int height)
    {
        if (vertices is { Length: >= 64 } polygon && width > 0 && height > 0)
        {
            int firstY = (int)Math.Clamp(Math.Ceiling(Bounds.Top), 0, height);
            int lastY = (int)Math.Clamp(Math.Floor(Bounds.Bottom), -1, height - 1);
            // Sorting only pays off when the same edges would be scanned across many rows.
            if ((long)lastY - firstY >= 64)
                return GetRunsWithActiveEdges(polygon, width, firstY, lastY);
        }
        return GetRunsByRow(width, height);
    }

    private IEnumerable<PixelRowRun> GetRunsByRow(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        int firstY = (int)Math.Clamp(Math.Ceiling(Bounds.Top), 0, height);
        int lastY = (int)Math.Clamp(Math.Floor(Bounds.Bottom), -1, height - 1);
        List<double> crossings = new();
        double cx = ellipseBounds.X + ellipseBounds.Width / 2, cy = ellipseBounds.Y + ellipseBounds.Height / 2;
        double rx = ellipseBounds.Width / 2, ry = ellipseBounds.Height / 2;
        double angle = rotation * Math.PI / 180, c = Math.Cos(angle), s = Math.Sin(angle);
        double a = c * c / (rx * rx) + s * s / (ry * ry);
        double b = 2 * c * s * (1 / (rx * rx) - 1 / (ry * ry));
        double d = s * s / (rx * rx) + c * c / (ry * ry);
        for (int y = firstY; y <= lastY; y++)
        {
            if (vertices == null)
            {
                double dy = y - cy;
                double determinant = b * b * dy * dy - 4 * a * (d * dy * dy - 1);
                if (determinant <= 0) continue;
                double halfRun = Math.Sqrt(determinant) / (2 * a);
                double middle = cx - b * dy / (2 * a);
                int start = (int)Math.Clamp(Math.Floor(middle - halfRun) + 1, 0, width);
                int end = (int)Math.Clamp(Math.Ceiling(middle + halfRun), 0, width);
                if (start < end) yield return new(y, start, end);
            }
            else
            {
                crossings.Clear();
                for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                {
                    Point from = vertices[j], to = vertices[i];
                    if ((from.Y <= y && y < to.Y) || (to.Y <= y && y < from.Y))
                        crossings.Add(from.X + (y - from.Y) / (to.Y - from.Y) * (to.X - from.X));
                }
                SortCrossings(crossings);
                for (int i = 0; i + 1 < crossings.Count; i += 2)
                {
                    int start = (int)Math.Clamp(Math.Ceiling(crossings[i]), 0, width);
                    int end = (int)Math.Clamp(Math.Ceiling(crossings[i + 1]), 0, width);
                    if (start < end) yield return new(y, start, end);
                }
            }
        }
    }

    private static IEnumerable<PixelRowRun> GetRunsWithActiveEdges(Point[] points, int width, int firstY, int lastY)
    {
        ScanEdge[] edges = ArrayPool<ScanEdge>.Shared.Rent(points.Length);
        try
        {
            int edgeCount = SortEdges(points, firstY, lastY, edges);
            int next = 0, activeCount = 0;
            List<double> crossings = new();
            for (int y = firstY; y <= lastY; y++)
            {
                // Active edges reuse the slots already consumed from the start-ordered array.
                while (next < edgeCount && edges[next].StartY <= y)
                    edges[activeCount++] = edges[next++];

                crossings.Clear();
                for (int i = 0; i < activeCount;)
                {
                    ScanEdge edge = edges[i];
                    if (edge.EndY <= y)
                    {
                        edges[i] = edges[--activeCount];
                        continue;
                    }

                    crossings.Add(edge.From.X + (y - edge.From.Y) / edge.DeltaY * edge.DeltaX);
                    i++;
                }

                SortCrossings(crossings);
                for (int i = 0; i + 1 < crossings.Count; i += 2)
                {
                    int start = (int)Math.Clamp(Math.Ceiling(crossings[i]), 0, width);
                    int end = (int)Math.Clamp(Math.Ceiling(crossings[i + 1]), 0, width);
                    if (start < end) yield return new(y, start, end);
                }
            }
        }
        finally
        {
            ArrayPool<ScanEdge>.Shared.Return(edges);
        }
    }

    private static void SortCrossings(List<double> crossings)
    {
        if (crossings.Count > 2)
            crossings.Sort();
        else if (crossings.Count == 2 && crossings[0].CompareTo(crossings[1]) > 0)
            (crossings[0], crossings[1]) = (crossings[1], crossings[0]);
    }

    private static int SortEdges(Point[] points, int firstY, int lastY, ScanEdge[] edges)
    {
        int count = 0;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            Point from = points[j], to = points[i];
            double startY = Math.Min(from.Y, to.Y);
            double endY = Math.Max(from.Y, to.Y);
            if (startY < endY && startY <= lastY && endY > firstY)
                edges[count++] = new(from, to.X - from.X, to.Y - from.Y, startY, endY);
        }
        Array.Sort(edges, 0, count);
        return count;
    }

    private readonly record struct ScanEdge(Point From, double DeltaX, double DeltaY, double StartY, double EndY) : IComparable<ScanEdge>
    {
        public int CompareTo(ScanEdge other) => StartY.CompareTo(other.StartY);
    }
}

public readonly record struct PixelRowRun(int Y, int StartX, int EndX);
