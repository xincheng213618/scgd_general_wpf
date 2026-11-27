using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ColorVision.Core;

internal static class FovSeedMedian
{
    // SIMD lanes represent adjacent 5x5 windows of finite seed intensities.
    // The destination is separate from the five overlapping source windows.
    public static bool TryWriteRow(ReadOnlySpan<double> row0, ReadOnlySpan<double> row1, ReadOnlySpan<double> row2,
        ReadOnlySpan<double> row3, ReadOnlySpan<double> row4, Span<double> destination)
    {
        int width = destination.Length;
        if (!Vector.IsHardwareAccelerated || width < Vector<double>.Count)
            return false;

        // Validate the four halo samples used by the unchecked vector loads.
        int required = checked(width + 4);
        if (row0.Length < required || row1.Length < required || row2.Length < required ||
            row3.Length < required || row4.Length < required)
            throw new ArgumentException("Seed median rows must include the two-pixel halo on both sides.");

        ref double p0 = ref MemoryMarshal.GetReference(row0);
        ref double p1 = ref MemoryMarshal.GetReference(row1);
        ref double p2 = ref MemoryMarshal.GetReference(row2);
        ref double p3 = ref MemoryMarshal.GetReference(row3);
        ref double p4 = ref MemoryMarshal.GetReference(row4);
        ref double output = ref MemoryMarshal.GetReference(destination);
        for (int x = 0; ; x += Vector<double>.Count)
        {
            // Reuse the last valid full vector for a partial tail. Loads end
            // at width + 3; the seed rows already include four halo pixels.
            int offset = Math.Min(x, width - Vector<double>.Count);
            Vector<double> result = SelectVector(ref p0, ref p1, ref p2, ref p3, ref p4, (nuint)offset);
            result.StoreUnsafe(ref output, (nuint)offset);
            if (offset == width - Vector<double>.Count) return true;
        }
    }

    private static Vector<double> SelectVector(ref double row0, ref double row1, ref double row2, ref double row3, ref double row4, nuint offset)
    {
        Vector<double> c0 = Vector.LoadUnsafe(ref row0, offset + 0);
        Vector<double> c1 = Vector.LoadUnsafe(ref row0, offset + 1);
        Vector<double> c2 = Vector.LoadUnsafe(ref row0, offset + 2);
        Vector<double> c3 = Vector.LoadUnsafe(ref row0, offset + 3);
        Vector<double> c4 = Vector.LoadUnsafe(ref row0, offset + 4);
        Vector<double> c5 = Vector.LoadUnsafe(ref row1, offset + 0);
        Vector<double> c6 = Vector.LoadUnsafe(ref row1, offset + 1);
        Vector<double> c7 = Vector.LoadUnsafe(ref row1, offset + 2);
        Vector<double> c8 = Vector.LoadUnsafe(ref row1, offset + 3);
        Vector<double> c9 = Vector.LoadUnsafe(ref row1, offset + 4);
        Vector<double> c10 = Vector.LoadUnsafe(ref row2, offset + 0);
        Vector<double> c11 = Vector.LoadUnsafe(ref row2, offset + 1);
        Vector<double> c12 = Vector.LoadUnsafe(ref row2, offset + 2);
        Vector<double> c13 = Vector.LoadUnsafe(ref row2, offset + 3);
        Vector<double> c14 = Vector.LoadUnsafe(ref row2, offset + 4);
        Vector<double> c15 = Vector.LoadUnsafe(ref row3, offset + 0);
        Vector<double> c16 = Vector.LoadUnsafe(ref row3, offset + 1);
        Vector<double> c17 = Vector.LoadUnsafe(ref row3, offset + 2);
        Vector<double> c18 = Vector.LoadUnsafe(ref row3, offset + 3);
        Vector<double> c19 = Vector.LoadUnsafe(ref row3, offset + 4);
        Vector<double> c20 = Vector.LoadUnsafe(ref row4, offset + 0);
        Vector<double> c21 = Vector.LoadUnsafe(ref row4, offset + 1);
        Vector<double> c22 = Vector.LoadUnsafe(ref row4, offset + 2);
        Vector<double> c23 = Vector.LoadUnsafe(ref row4, offset + 3);
        Vector<double> c24 = Vector.LoadUnsafe(ref row4, offset + 4);
        SortEight(ref c0, ref c1, ref c2, ref c3, ref c4, ref c5, ref c6, ref c7);
        SortEight(ref c8, ref c9, ref c10, ref c11, ref c12, ref c13, ref c14, ref c15);
        SortEight(ref c16, ref c17, ref c18, ref c19, ref c20, ref c21, ref c22, ref c23);

        // Odd-even merge the first two eights. Only ranks at indices 3..12
        // are requested below, so the two outer cross-pair exchanges are dead.
        MergeEight(ref c0, ref c2, ref c4, ref c6, ref c8, ref c10, ref c12, ref c14);
        MergeEight(ref c1, ref c3, ref c5, ref c7, ref c9, ref c11, ref c13, ref c15);
        Exchange(ref c3, ref c4);
        Exchange(ref c5, ref c6);
        Exchange(ref c7, ref c8);
        Exchange(ref c9, ref c10);
        Exchange(ref c11, ref c12);

        // Select ranks 12 and 13 of the 24 sorted values (one list of 16
        // and one of 8) as max{min(nextA,nextB)} over all possible cuts.
        // The median after adding c24 is c24 clamped between those ranks.
        Vector<double> lower = c3;
        Vector<double> upper = c4;
        IncludeCut(ref lower, ref upper, c4, c5, c23);
        IncludeCut(ref lower, ref upper, c5, c6, c22);
        IncludeCut(ref lower, ref upper, c6, c7, c21);
        IncludeCut(ref lower, ref upper, c7, c8, c20);
        IncludeCut(ref lower, ref upper, c8, c9, c19);
        IncludeCut(ref lower, ref upper, c9, c10, c18);
        IncludeCut(ref lower, ref upper, c10, c11, c17);
        IncludeCut(ref lower, ref upper, c11, c12, c16);
        return Max(lower, Min(c24, upper));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void IncludeCut(ref Vector<double> lower, ref Vector<double> upper,
        Vector<double> a0, Vector<double> a1, Vector<double> b)
    {
        lower = Max(lower, Min(a0, b));
        upper = Max(upper, Min(a1, b));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SortEight(ref Vector<double> a, ref Vector<double> b, ref Vector<double> c, ref Vector<double> d,
        ref Vector<double> e, ref Vector<double> f, ref Vector<double> g, ref Vector<double> h)
    {
        SortFour(ref a, ref b, ref c, ref d);
        SortFour(ref e, ref f, ref g, ref h);
        MergeEight(ref a, ref b, ref c, ref d, ref e, ref f, ref g, ref h);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SortFour(ref Vector<double> a, ref Vector<double> b, ref Vector<double> c, ref Vector<double> d)
    {
        Exchange(ref a, ref b);
        Exchange(ref c, ref d);
        MergeFour(ref a, ref b, ref c, ref d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MergeEight(ref Vector<double> a, ref Vector<double> b, ref Vector<double> c, ref Vector<double> d,
        ref Vector<double> e, ref Vector<double> f, ref Vector<double> g, ref Vector<double> h)
    {
        MergeFour(ref a, ref c, ref e, ref g);
        MergeFour(ref b, ref d, ref f, ref h);
        Exchange(ref b, ref c);
        Exchange(ref d, ref e);
        Exchange(ref f, ref g);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MergeFour(ref Vector<double> a, ref Vector<double> b, ref Vector<double> c, ref Vector<double> d)
    {
        Exchange(ref a, ref c);
        Exchange(ref b, ref d);
        Exchange(ref b, ref c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<double> Min(Vector<double> a, Vector<double> b)
#if NET9_0_OR_GREATER
        => Vector.MinNative(a, b);
#else
        => Vector.Min(a, b);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<double> Max(Vector<double> a, Vector<double> b)
#if NET9_0_OR_GREATER
        => Vector.MaxNative(a, b);
#else
        => Vector.Max(a, b);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Exchange(ref Vector<double> a, ref Vector<double> b)
    {
        Vector<double> lower = Min(a, b);
        b = Max(a, b);
        a = lower;
    }
}
