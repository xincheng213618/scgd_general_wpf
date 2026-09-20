using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Cie;

public sealed record CieGamutComparison(double SampleArea, double ReferenceArea, double IntersectionArea,
    double AreaRatioPercent, double CoveragePercent, IReadOnlyList<CieChromaticity> IntersectionXy);

public readonly record struct CieWavelengthResult(double Wavelength, bool IsComplementary, double Purity,
    CieChromaticity Boundary, CieChromaticity SpectralPoint);

public static class CieGamutGeometry
{
    private static double Cross(CieChromaticity a, CieChromaticity b, CieChromaticity p) =>
        (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    private static double SignedArea(IReadOnlyList<CieChromaticity> points)
    {
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            CieChromaticity a = points[i], b = points[(i + 1) % points.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return area / 2;
    }

    public static CieGamutComparison Compare(IReadOnlyList<CieChromaticity> sample,
        IReadOnlyList<CieChromaticity> reference, CieDiagramKind kind)
    {
        if (sample.Count != 3 || reference.Count != 3 || sample.Concat(reference).Any(p => !p.IsFinite))
            throw new ArgumentException("色域计算需要两组三个有限的原色色坐标。");
        CieDiagramProfile profile = CieDiagramProfiles.Get(kind);
        List<CieChromaticity> s = sample.Select(profile.ToDiagramPoint).ToList();
        List<CieChromaticity> r = reference.Select(profile.ToDiagramPoint).ToList();
        if (s.Concat(r).Any(p => !p.IsFinite)) throw new ArgumentException("色坐标不能转换到所选平面。");
        double sa = Math.Abs(SignedArea(s)), ra = Math.Abs(SignedArea(r));
        if (!double.IsFinite(sa) || !double.IsFinite(ra) || !double.IsFinite(sa / ra)) throw new ArgumentException("色域坐标超出可计算范围。");
        if (sa < 1e-12 || ra < 1e-12) throw new ArgumentException("三原色不能重合或共线。");
        double winding = Math.Sign(SignedArea(r));
        List<CieChromaticity> intersection = s;
        for (int edge = 0; edge < r.Count && intersection.Count > 0; edge++)
        {
            CieChromaticity a = r[edge], b = r[(edge + 1) % r.Count];
            List<CieChromaticity> input = intersection;
            intersection = new();
            CieChromaticity previous = input[^1];
            double prevSide = winding * Cross(a, b, previous);
            foreach (CieChromaticity current in input)
            {
                double side = winding * Cross(a, b, current);
                if ((side >= 0) != (prevSide >= 0))
                {
                    double t = prevSide / (prevSide - side);
                    intersection.Add(new(previous.X + t * (current.X - previous.X), previous.Y + t * (current.Y - previous.Y)));
                }
                if (side >= 0) intersection.Add(current);
                previous = current;
                prevSide = side;
            }
        }
        double ia = Math.Min(Math.Abs(SignedArea(intersection)), Math.Min(sa, ra));
        return new(sa, ra, ia, sa / ra * 100, Math.Clamp(ia / ra * 100, 0, 100), intersection.Select(profile.FromDiagramPoint).ToArray());
    }

    public static bool Contains(CieChromaticity point, IReadOnlyList<CieChromaticity> polygon)
    {
        if (!point.IsFinite || polygon.Count < 3) return false;
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            CieChromaticity a = polygon[j], b = polygon[i];
            if (Math.Abs(Cross(a, b, point)) < 1e-10 &&
                point.X >= Math.Min(a.X, b.X) - 1e-10 && point.X <= Math.Max(a.X, b.X) + 1e-10 &&
                point.Y >= Math.Min(a.Y, b.Y) - 1e-10 && point.Y <= Math.Max(a.Y, b.Y) + 1e-10) return true;
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    public static CieWavelengthResult? Wavelength(CieChromaticity sample, CieChromaticity white, IReadOnlyList<CieSpectrumPoint> locus)
    {
        CieChromaticity[] boundary = locus.Select(p => p.Chromaticity).ToArray();
        if (!Contains(sample, boundary) || !Contains(white, boundary) || CieAnalysisMath.Distance(sample, white) < 1e-8) return null;
        double dx = sample.X - white.X, dy = sample.Y - white.Y;
        var hit = RayHit(white, dx, dy, locus, true);
        if (hit == null || hit.Value.T < 1 - 1e-7) return null;
        bool complementary = hit.Value.Index == locus.Count - 1;
        var spectral = complementary ? RayHit(white, -dx, -dy, locus, false) : hit;
        if (spectral == null) return null;
        int i = spectral.Value.Index;
        double wave = locus[i].Wavelength + spectral.Value.Fraction * (locus[i + 1].Wavelength - locus[i].Wavelength);
        return new(wave, complementary, Math.Clamp(1 / hit.Value.T, 0, 1), hit.Value.Point, spectral.Value.Point);
    }

    private static (int Index, double Fraction, double T, CieChromaticity Point)? RayHit(CieChromaticity origin, double dx, double dy,
        IReadOnlyList<CieSpectrumPoint> locus, bool includePurple)
    {
        (int Index, double Fraction, double T, CieChromaticity Point)? best = null;
        int count = includePurple ? locus.Count : locus.Count - 1;
        for (int i = 0; i < count; i++)
        {
            CieChromaticity a = locus[i].Chromaticity, b = locus[(i + 1) % locus.Count].Chromaticity;
            double ex = b.X - a.X, ey = b.Y - a.Y, determinant = dx * ey - dy * ex;
            if (Math.Abs(determinant) < 1e-14) continue;
            double ax = a.X - origin.X, ay = a.Y - origin.Y;
            double t = (ax * ey - ay * ex) / determinant;
            double u = (ax * dy - ay * dx) / determinant;
            if (t > 1e-10 && u >= -1e-9 && u <= 1 + 1e-9 && (best == null || t < best.Value.T))
                best = (i, Math.Clamp(u, 0, 1), t, new(origin.X + t * dx, origin.Y + t * dy));
        }
        return best;
    }
}
