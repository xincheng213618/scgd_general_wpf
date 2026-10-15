using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

public sealed partial class DisplayMetrologyProvider
{
    private const int RingTraceSectors = 72;
    private static IEnumerable<RingBand> FindGhostSeeds(double[] profile, double[] coarseProfile,
        int start, int end, double requiredProminence)
    {
        var coarseBands = FindRingPeaks(coarseProfile, start, end)
            .Select(peak => FindRingCandidateBand(coarseProfile, peak, start, end))
            .Where(band => band.Prominence >= requiredProminence).ToArray();
        foreach (int peak in FindRingPeaks(profile, start, end))
        {
            RingBand band = FindRingCandidateBand(profile, peak, start, end);
            if (band.Prominence < requiredProminence || band.Right - band.Left < 2
                || band.Left <= start || band.Right >= end) continue;
            // Confirm the region at a second scale, allowing the top of a broad plateau to drift.
            if (coarseBands.Any(coarse => coarse.Left <= band.Right && band.Left <= coarse.Right)) yield return band;
        }
    }

    private static RingReading RefineGhostRing(float[] signal, int width, int height, RotatedRect primary,
        double[] polar, int length, RingBand seed, int start, int end, double step,
        double requiredProminence, double primaryInnerRadius, CancellationToken token)
    {
        var reading = TryRefineGhostRing(signal, width, height, primary, polar, length, seed, start, end,
            step, requiredProminence, primaryInnerRadius, preferNearest: false, token);
        // A brighter neighbouring ring must not steal the trace of a weaker candidate.
        return reading.Region.HasValue ? reading : TryRefineGhostRing(signal, width, height, primary, polar,
            length, seed, start, end, step, requiredProminence, primaryInnerRadius, preferNearest: true, token);
    }

    private static RingReading TryRefineGhostRing(float[] signal, int width, int height, RotatedRect primary,
        double[] polar, int length, RingBand seed, int start, int end, double step,
        double requiredProminence, double primaryInnerRadius, bool preferNearest, CancellationToken token)
    {
        var original = ReadRingBand(polar, length, seed, requiredProminence * .2);
        double primaryRadius = (primary.Size.Width + primary.Size.Height) / 4;
        int padding = Math.Max(seed.Right - seed.Left, (int)(primaryRadius * .1 / step));
        int traceEnd = Math.Min(length - 2, (int)(primaryInnerRadius / step) - 1);
        const int sectors = RingTraceSectors, anglesPerSector = RingAngles / sectors;
        var points = new List<Point2f>();
        var sectorProfile = new double[length];
        var samples = new double[anglesPerSector];
        for (int sector = 0; sector < sectors; sector++)
        {
            token.ThrowIfCancellationRequested();
            for (int r = 0; r < length; r++)
            {
                for (int a = 0; a < anglesPerSector; a++) samples[a] = polar[(sector * anglesPerSector + a) * length + r];
                sectorProfile[r] = RingMedian(samples);
            }
            double[] smooth = SmoothRingProfile(sectorProfile, Math.Max(1, primaryRadius * .0025 / step));
            RingBand? nearest = null;
            // A displaced ring can create two radial horns. Search the complete inner ray;
            // a window around either horn would miss the opposite side of the same ring.
            foreach (int peak in FindRingPeaks(smooth, start, traceEnd))
            {
                RingBand band = FindRingCandidateBand(smooth, peak, start, traceEnd);
                if (band.Prominence < requiredProminence || band.Right - band.Left < 2
                    || band.Left <= start || band.Right >= traceEnd) continue;
                // Preserve the ring's identity when another brighter ring is nearby.
                if (nearest == null || (preferNearest
                    ? Math.Abs((band.Left + band.Right) * .5 - seed.Peak) < Math.Abs((nearest.Left + nearest.Right) * .5 - seed.Peak)
                    : band.Prominence > nearest.Prominence)) nearest = band;
            }
            if (nearest == null) continue;
            // Use the band centre: a flat maximum can wander across a broad ring under tiny noise.
            double radial = (nearest.Left + nearest.Right) * .5 * step;
            double angle = (sector * anglesPerSector + (anglesPerSector - 1) * .5) * 2 * Math.PI / RingAngles;
            var point = RingPoint(primary, radial, angle);
            points.Add(new Point2f((float)point.X, (float)point.Y));
        }
        if (points.Count < sectors * 2 / 3) return original;

        RotatedRect? robust = FitGhostEllipse(points, primaryRadius, Math.Max(2 * step, (seed.Right - seed.Left) * step * .1));
        if (!robust.HasValue) return original;
        RotatedRect fit = robust.Value;
        double residual = 0;
        for (int iteration = 0; iteration < 2; iteration++)
        {
            if (!IsUsableGhostEllipse(fit, primaryRadius)) return original;
            double radius = (fit.Size.Width + fit.Size.Height) / 4;
            double[] errors = points.Select(p => Math.Abs(RingRadiusAt(fit, p.X, p.Y) - radius)).ToArray();
            residual = RingMedian((double[])errors.Clone());
            double cutoff = Math.Max(2 * step, 4 * residual);
            var retained = points.Where((p, index) => errors[index] <= cutoff).ToList();
            if (retained.Count < sectors * 2 / 3) return original;
            points = retained;
            fit = Cv2.FitEllipse(points);
        }
        if (!IsUsableGhostEllipse(fit, primaryRadius)) return original;
        double fittedRadius = (fit.Size.Width + fit.Size.Height) / 4;
        if (Math.Abs(fittedRadius - seed.Peak * step) > padding * step)
            return original;
        for (int i = 0; i < sectors; i++)
        {
            var point = RingPoint(fit, fittedRadius * 1.3, i * 2 * Math.PI / sectors);
            if (point.X < 1 || point.Y < 1 || point.X >= width - 1 || point.Y >= height - 1) return original;
        }

        // A concentric ring already has an unbiased geometry. Require shape displacement
        // larger than the fitted trace uncertainty before changing its measurement coordinates.
        double[] radii = Enumerable.Range(0, sectors).Select(i =>
        {
            var point = RingPoint(fit, (fit.Size.Width + fit.Size.Height) / 4, i * 2 * Math.PI / sectors);
            return RingRadiusAt(primary, point.X, point.Y);
        }).ToArray();
        double average = radii.Average();
        double displacement = Math.Sqrt(radii.Average(r => (r - average) * (r - average)));
        if (displacement <= Math.Max(2 * step, 3 * residual)) return original;

        int refinedLength = (int)Math.Ceiling((fit.Size.Width + fit.Size.Height) / 4 * 1.3 / step) + 1;
        double[] refinedPolar = SampleRing(signal, width, height, fit, step, refinedLength, token);
        var profile = new double[refinedLength];
        var values = new double[RingAngles];
        for (int r = 0; r < refinedLength; r++)
        {
            for (int a = 0; a < RingAngles; a++) values[a] = refinedPolar[a * refinedLength + r];
            profile[r] = RingMedian(values);
        }
        profile = SmoothRingProfile(profile, Math.Max(1, primaryRadius * .0025 / step));
        int expected = (int)Math.Round((fit.Size.Width + fit.Size.Height) / 4 / step);
        int searchLeft = Math.Max(3, expected - padding), searchRight = Math.Min(refinedLength - 2, expected + padding);
        var refinedBands = FindRingPeaks(profile, searchLeft, searchRight)
            .Select(peak => FindRingCandidateBand(profile, peak, searchLeft, searchRight))
            .Where(band => band.Prominence >= requiredProminence && band.Left > searchLeft
                && band.Right < searchRight && band.Right - band.Left >= 2).ToArray();
        if (refinedBands.Length == 0) return original;
        // The primary-ring halo can rise above the weak peak near the search boundary.
        // Select an actual local peak belonging to this fitted ring, not that rising shoulder.
        RingBand refinedBand = refinedBands.MinBy(band => Math.Abs(band.Peak - expected))!;
        // The independently fitted region must stay inside the main image, not sample its bright edge.
        for (int i = 0; i < sectors; i++)
        {
            var point = RingPoint(fit, refinedBand.Right * step, i * 2 * Math.PI / sectors);
            if (RingRadiusAt(primary, point.X, point.Y) >= primaryInnerRadius) return original;
        }
        var reading = ReadRingBand(refinedPolar, refinedLength, refinedBand, requiredProminence * .2, localBackground: true);
        // A nearby primary halo can raise the shared radial background on one side of a
        // displaced ring. Use the independently tracked sectors for its support check.
        double coverage = Math.Min((double)points.Count / sectors, reading.Coverage);
        return coverage >= 2.0 / 3 ? reading with { Region = fit, Coverage = coverage } : original;
    }

    private static bool IsUsableGhostEllipse(RotatedRect ellipse, double primaryRadius)
        => float.IsFinite(ellipse.Center.X) && float.IsFinite(ellipse.Center.Y)
            && float.IsFinite(ellipse.Size.Width) && float.IsFinite(ellipse.Size.Height)
            && ellipse.Size.Width > 4 && ellipse.Size.Height > 4
            && Math.Min(ellipse.Size.Width, ellipse.Size.Height) / Math.Max(ellipse.Size.Width, ellipse.Size.Height) >= .6
            && Math.Max(ellipse.Size.Width, ellipse.Size.Height) < primaryRadius * 2;

    private static RotatedRect? FitGhostEllipse(List<Point2f> points, double primaryRadius, double tolerance)
    {
        // Neighbouring rings or dust can supply an occasional wrong radial peak. A plain
        // least-squares ellipse lets those points move the whole region; use bounded consensus first.
        var random = new Random(7331);
        RotatedRect? best = null;
        int bestCount = 0;
        double bestError = double.PositiveInfinity;
        var selected = new HashSet<int>();
        for (int attempt = 0; attempt < 64; attempt++)
        {
            selected.Clear();
            while (selected.Count < 6) selected.Add(random.Next(points.Count));
            RotatedRect ellipse = Cv2.FitEllipse(selected.Select(i => points[i]));
            if (!IsUsableGhostEllipse(ellipse, primaryRadius)) continue;
            double radius = (ellipse.Size.Width + ellipse.Size.Height) / 4;
            double[] errors = points.Select(p => Math.Abs(RingRadiusAt(ellipse, p.X, p.Y) - radius)).ToArray();
            int count = errors.Count(e => e <= tolerance);
            double error = errors.Where(e => e <= tolerance).Sum();
            if (count > bestCount || count == bestCount && error < bestError)
            { best = ellipse; bestCount = count; bestError = error; }
        }
        return bestCount >= RingTraceSectors * 2 / 3 ? best : null;
    }

    private static double RingRadiusAt(RotatedRect ellipse, double x, double y)
    {
        double rotation = ellipse.Angle * Math.PI / 180, cos = Math.Cos(rotation), sin = Math.Sin(rotation);
        double dx = x - ellipse.Center.X, dy = y - ellipse.Center.Y;
        double u = (dx * cos + dy * sin) / (ellipse.Size.Width / 2), v = (-dx * sin + dy * cos) / (ellipse.Size.Height / 2);
        return Math.Sqrt(u * u + v * v) * (ellipse.Size.Width + ellipse.Size.Height) / 4;
    }
}
