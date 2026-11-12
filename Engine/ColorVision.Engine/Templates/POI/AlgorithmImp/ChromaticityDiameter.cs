using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp;

internal static class ChromaticityDiameter
{
    private const int LeafSize = 16;
    private static readonly IComparer<Sample> ByU = Comparer<Sample>.Create((a, b) => a.U.CompareTo(b.U));
    private static readonly IComparer<Sample> ByV = Comparer<Sample>.Create((a, b) => a.V.CompareTo(b.V));

    public static double Calculate(IList<PoiResultCIExyuvData> items)
    {
        if (items.Count <= LeafSize)
        {
            double max = 0;
            for (int i = 0; i < items.Count; i++)
            {
                Sample first = new(items[i].u, items[i].v);
                for (int j = i + 1; j < items.Count; j++)
                {
                    double distance = SquaredDistance(first, new Sample(items[j].u, items[j].v));
                    if (distance > max) max = distance;
                }
            }
            return Math.Sqrt(max);
        }

        var points = new Sample[items.Count];
        int count = 0;
        int seenInfinityPatterns = 0;
        foreach (var item in items)
        {
            double u = item.u, v = item.v;
            // Legacy comparisons ignore any pair producing NaN.
            if (double.IsNaN(u) || double.IsNaN(v)) continue;
            int pattern = (double.IsPositiveInfinity(u) ? 1 : 0) | (double.IsNegativeInfinity(u) ? 2 : 0)
                | (double.IsPositiveInfinity(v) ? 4 : 0) | (double.IsNegativeInfinity(v) ? 8 : 0);
            if (pattern == 0)
            {
                if (seenInfinityPatterns != 0) return double.PositiveInfinity;
                points[count++] = new Sample(u, v);
                continue;
            }

            if (count != 0) return double.PositiveInfinity;
            int bit = 1 << pattern;
            if ((seenInfinityPatterns & bit) != 0) continue;
            // Opposite or finite partners produce infinity unless one coordinate subtracts identical infinities.
            for (int other = 1; other < 16; other++)
                if ((seenInfinityPatterns & (1 << other)) != 0 && (pattern & other) == 0)
                    return double.PositiveInfinity;
            seenInfinityPatterns |= bit;
        }

        if (count < 2) return 0;
        Partition root = BuildPartition(points, 0, count);
        return Math.Sqrt(Search(points, root, root, 0));
    }

    private static Partition BuildPartition(Sample[] points, int start, int count)
    {
        double minU = points[start].U, maxU = minU;
        double minV = points[start].V, maxV = minV;
        for (int i = start + 1; i < start + count; i++)
        {
            Sample point = points[i];
            minU = Math.Min(minU, point.U);
            maxU = Math.Max(maxU, point.U);
            minV = Math.Min(minV, point.V);
            maxV = Math.Max(maxV, point.V);
        }
        Sample min = new(minU, minV), max = new(maxU, maxV);
        if (min == max) return new Partition(start, 1, min, max);
        if (count <= LeafSize) return new Partition(start, count, min, max);

        Array.Sort(points, start, count, maxU - minU >= maxV - minV ? ByU : ByV);
        int half = count / 2;
        return new Partition(start, count, min, max,
            BuildPartition(points, start, half), BuildPartition(points, start + half, count - half));
    }

    private static double Search(Sample[] points, Partition first, Partition second, double best)
    {
        if (UpperBound(first, second) <= best) return best;

        bool same = ReferenceEquals(first, second);
        if (same && first.Left is { } leftHalf)
        {
            Partition rightHalf = first.Right!;
            best = Search(points, leftHalf, rightHalf, best);
            best = Search(points, leftHalf, leftHalf, best);
            return Search(points, rightHalf, rightHalf, best);
        }

        if (first.Left != null || second.Left != null)
        {
            if (first.Left == null || (second.Left != null && second.Count > first.Count))
                (first, second) = (second, first);
            Partition left = first.Left!, right = first.Right!;
            if (UpperBound(left, second) < UpperBound(right, second)) (left, right) = (right, left);
            best = Search(points, left, second, best);
            return Search(points, right, second, best);
        }

        for (int i = first.Start; i < first.Start + first.Count; i++)
        {
            for (int j = same ? i + 1 : second.Start; j < second.Start + second.Count; j++)
                best = Math.Max(best, SquaredDistance(points[i], points[j]));
        }
        return best;
    }

    // Each coordinate difference is bounded using original samples and the same double arithmetic as a point pair.
    private static double UpperBound(Partition a, Partition b)
    {
        double u = Math.Max(Math.Abs(a.Min.U - b.Max.U), Math.Abs(a.Max.U - b.Min.U));
        double v = Math.Max(Math.Abs(a.Min.V - b.Max.V), Math.Abs(a.Max.V - b.Min.V));
        return u * u + v * v;
    }

    private static double SquaredDistance(Sample a, Sample b)
    {
        double u = a.U - b.U, v = a.V - b.V;
        return u * u + v * v;
    }

    private readonly record struct Sample(double U, double V);
    private sealed record Partition(int Start, int Count, Sample Min, Sample Max, Partition? Left = null, Partition? Right = null);
}
