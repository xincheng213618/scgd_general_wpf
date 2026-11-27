using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

internal sealed class PolygonScanlines
{
    // Bound scratch storage independently of the source image and input vertex count.
    private const int MaximumRows = 65_536;
    private const int MaximumRuns = 131_072;
    private const int MaximumEdges = 4096;

    private readonly int _firstY;
    private readonly int[] _offsets;
    private readonly (int Start, int End)[] _runs;

    private PolygonScanlines(int firstY, int[] offsets, (int Start, int End)[] runs, long pixelCount)
    {
        _firstY = firstY;
        _offsets = offsets;
        _runs = runs;
        PixelCount = pixelCount;
    }

    internal long PixelCount { get; }

    internal ReadOnlySpan<(int Start, int End)> GetRuns(int y)
    {
        int row = y - _firstY;
        return _runs.AsSpan(_offsets[row], _offsets[row + 1] - _offsets[row]);
    }

    internal bool Contains(int x, int y)
    {
        int row = y - _firstY;
        int low = _offsets[row], high = _offsets[row + 1] - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            var (start, end) = _runs[middle];
            if (x < start) high = middle - 1;
            else if (x >= end) low = middle + 1;
            else return true;
        }
        return false;
    }

    internal static PolygonScanlines? Create(IReadOnlyList<AlgorithmPoint> points, int firstX, int firstY,
        int endX, int endY, double tolerance, CancellationToken token)
    {
        int height = endY - firstY;
        if (endX - firstX < 32 || height is <= 0 or > MaximumRows || points.Count > MaximumEdges)
            return null;
        // Extreme vertices keep the original per-pixel calculation.
        foreach (var point in points)
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || Math.Abs(point.X) > int.MaxValue || Math.Abs(point.Y) > int.MaxValue)
                return null;

        var edges = new Edge[points.Count];
        for (int i = 0, previous = points.Count - 1; i < edges.Length; previous = i++)
        {
            var a = points[previous];
            var b = points[i];
            edges[i] = new Edge(a, b, b.X - a.X, b.Y - a.Y,
                Math.Min(a.Y, b.Y) - tolerance, Math.Max(a.Y, b.Y) + tolerance,
                (int)Math.Clamp(Math.Ceiling(Math.Min(a.X, b.X) - tolerance), firstX, endX),
                (int)Math.Clamp(Math.Floor(Math.Max(a.X, b.X) + tolerance) + 1, firstX, endX));
        }

        var offsets = new int[height + 1];
        var runs = new List<(int Start, int End)>(height);
        var events = new RowEvent[edges.Length * 3];
        long pixelCount = 0;
        for (int row = 0; row < height; row++)
        {
            token.ThrowIfCancellationRequested();
            offsets[row] = runs.Count;
            int y = firstY + row;
            bool inside = false;
            int eventCount = 0;
            foreach (var edge in edges)
            {
                // For an integer column, x < crossing is equivalent to x < ceil(crossing).
                if ((edge.A.Y > y) != (edge.B.Y > y))
                {
                    double crossing = edge.DeltaX * (y - edge.A.Y) / edge.DeltaY + edge.A.X;
                    if (crossing > firstX)
                    {
                        inside = !inside;
                        events[eventCount++] = new((int)Math.Min(Math.Ceiling(crossing), endX), 0);
                    }
                }
                var (start, end) = BoundaryRun(edge, y, tolerance);
                if (start == end) continue;
                events[eventCount++] = new(start, 1);
                events[eventCount++] = new(end, -1);
            }

            // Merge crossings and inclusive boundary runs before changing pixel membership.
            events.AsSpan(0, eventCount).Sort();
            int runStart = firstX;
            int boundaryDepth = 0;
            for (int i = 0; i < eventCount;)
            {
                int x = events[i].X;
                bool included = inside || boundaryDepth > 0;
                do
                {
                    int delta = events[i++].BoundaryDelta;
                    if (delta == 0) inside = !inside;
                    else boundaryDepth += delta;
                }
                while (i < eventCount && events[i].X == x);
                bool nextIncluded = inside || boundaryDepth > 0;
                if (!included && nextIncluded) runStart = x;
                else if (included && !nextIncluded && x > runStart)
                {
                    if (runs.Count == MaximumRuns) return null;
                    runs.Add((runStart, x));
                    pixelCount += x - runStart;
                }
            }
        }
        offsets[height] = runs.Count;
        return new PolygonScanlines(firstY, offsets, runs.ToArray(), pixelCount);
    }

    private static (int Start, int End) BoundaryRun(Edge edge, int y, double tolerance)
    {
        if (edge.StartX == edge.EndX || y < edge.LowerY || y > edge.UpperY) return default;
        double contribution = (y - edge.A.Y) * edge.DeltaX;
        if (edge.DeltaY == 0)
            return Math.Abs(contribution) <= tolerance ? (edge.StartX, edge.EndX) : default;

        // The original cross-product test is monotonic across one row, even with rounded
        // coordinates. Search its exact tolerance endpoints to retain boundary samples.
        double direction = Math.Sign(edge.DeltaY);
        double SignedCrossAt(int x) => ((x - edge.A.X) * edge.DeltaY - contribution) * direction;
        if (SignedCrossAt(edge.StartX) > tolerance || SignedCrossAt(edge.EndX - 1) < -tolerance) return default;

        int low = edge.StartX, high = edge.EndX;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (SignedCrossAt(mid) < -tolerance) low = mid + 1;
            else high = mid;
        }
        int start = low;
        high = edge.EndX;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (SignedCrossAt(mid) <= tolerance) low = mid + 1;
            else high = mid;
        }
        return (start, low);
    }

    private readonly record struct Edge(AlgorithmPoint A, AlgorithmPoint B, double DeltaX, double DeltaY,
        double LowerY, double UpperY, int StartX, int EndX);

    // Zero represents a ray-crossing toggle. +1 and -1 delimit inclusive geometric boundaries.
    private readonly record struct RowEvent(int X, int BoundaryDelta) : IComparable<RowEvent>
    {
        public int CompareTo(RowEvent other) => X.CompareTo(other.X);
    }
}
