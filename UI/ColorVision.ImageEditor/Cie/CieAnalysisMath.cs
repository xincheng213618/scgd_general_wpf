using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Cie;

public readonly record struct CieLab(double L, double A, double B);
public readonly record struct CieLuv(double L, double U, double V);

/// <summary>Colour differences use a shared reference white, without implicit chromatic adaptation.</summary>
public static class CieAnalysisMath
{
    private const double Epsilon = 216.0 / 24389;
    private const double Kappa = 24389.0 / 27;
    private static readonly Lazy<(double Temperature, CieChromaticity Uv)[]> Planckian = new(() =>
    {
        var samples = new List<(double, CieChromaticity)>();
        for (int t = 1667; t < 25000; t += 10)
            samples.Add((t, CieColorConverter.XyToCie1960uv(CieColorConverter.CctToApproximatePlanckianXy(t))));
        samples.Add((25000, CieColorConverter.XyToCie1960uv(CieColorConverter.CctToApproximatePlanckianXy(25000))));
        return samples.ToArray();
    });

    public static CieXyz XyYToXyz(double x, double y, double luminance)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(luminance) ||
            x < 0 || y <= 0 || x + y > 1.000000001 || luminance < 0)
            throw new ArgumentException("xyY 需要有限数值、x ≥ 0、y > 0、x + y ≤ 1、Y ≥ 0。");
        return new CieXyz(x * luminance / y, luminance, (1 - x - y) * luminance / y);
    }

    public static void ValidateXyz(CieXyz xyz)
    {
        if (!xyz.IsFinite || xyz.X < -1e-9 || xyz.Y < 0 || xyz.Z < -1e-9 ||
            !double.IsFinite(xyz.X + xyz.Y + xyz.Z))
            throw new ArgumentException("XYZ 必须是有限的非负数值。");
    }

    public static CieLab XyzToLab(CieXyz xyz, CieXyz white)
    {
        ValidateWhite(white);
        double fx = LabFunction(xyz.X / white.X);
        double fy = LabFunction(xyz.Y / white.Y);
        double fz = LabFunction(xyz.Z / white.Z);
        return new CieLab(116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    public static CieXyz LabToXyz(CieLab lab, CieXyz white)
    {
        ValidateWhite(white);
        double fy = (lab.L + 16) / 116;
        return new CieXyz(white.X * InverseLabFunction(fy + lab.A / 500),
            white.Y * InverseLabFunction(fy), white.Z * InverseLabFunction(fy - lab.B / 200));
    }

    public static CieLuv XyzToLuv(CieXyz xyz, CieXyz white)
    {
        ValidateWhite(white);
        double l = 116 * LabFunction(xyz.Y / white.Y) - 16;
        if (Math.Abs(l) < 1e-12) return new CieLuv(0, 0, 0);
        CieChromaticity uv = CieColorConverter.XyzToCie1976uv(xyz);
        CieChromaticity wn = CieColorConverter.XyzToCie1976uv(white);
        return new CieLuv(l, 13 * l * (uv.X - wn.X), 13 * l * (uv.Y - wn.Y));
    }

    public static CieXyz LuvToXyz(CieLuv luv, CieXyz white)
    {
        ValidateWhite(white);
        if (luv.L == 0 && luv.U == 0 && luv.V == 0) return new CieXyz(0, 0, 0);
        if (luv.L <= 0) throw new ArgumentException("L* 为零时 u*、v* 也必须为零；L* 不能小于零。");
        CieChromaticity wn = CieColorConverter.XyzToCie1976uv(white);
        CieChromaticity xy = CieColorConverter.Uv1976ToXy(new(luv.U / (13 * luv.L) + wn.X, luv.V / (13 * luv.L) + wn.Y));
        return XyYToXyz(xy.X, xy.Y, white.Y * InverseLabFunction((luv.L + 16) / 116));
    }

    private static void ValidateWhite(CieXyz white)
    {
        if (!white.IsFinite || white.X <= 0 || white.Y <= 0 || white.Z <= 0)
            throw new ArgumentException("参考白 XYZ 必须是有限的正数。");
    }

    private static double LabFunction(double t) => t > Epsilon ? Math.Cbrt(t) : (Kappa * t + 16) / 116;
    private static double InverseLabFunction(double t) => t * t * t > Epsilon ? t * t * t : (116 * t - 16) / Kappa;
    private static double Square(double x) => x * x;
    private static double Cos(double degrees) => Math.Cos(degrees * Math.PI / 180);
    private static double Sin(double degrees) => Math.Sin(degrees * Math.PI / 180);
    private static double Hue(double a, double b)
    {
        double angle = Math.Atan2(b, a) * 180 / Math.PI;
        return angle < 0 ? angle + 360 : angle;
    }
    public static double Chroma(CieLab lab) => Math.Sqrt(Square(lab.A) + Square(lab.B));
    public static double Hue(CieLab lab) => Chroma(lab) > 1e-10 ? Hue(lab.A, lab.B) : double.NaN;
    public static double DeltaE76(CieLab a, CieLab b) => Math.Sqrt(Square(a.L - b.L) + Square(a.A - b.A) + Square(a.B - b.B));
    public static double DeltaLuv(CieLuv a, CieLuv b) => Math.Sqrt(Square(a.L - b.L) + Square(a.U - b.U) + Square(a.V - b.V));

    // Sharma, Wu & Dalal (2005), eqs. 2–22. kL=kC=kH=1; supplementary 34-pair data are tested.
    public static double DeltaE2000(CieLab reference, CieLab sample)
    {
        double c1 = Chroma(reference), c2 = Chroma(sample);
        double meanC7 = Math.Pow((c1 + c2) / 2, 7);
        double g = 0.5 * (1 - Math.Sqrt(meanC7 / (meanC7 + Math.Pow(25, 7))));
        double a1 = (1 + g) * reference.A, a2 = (1 + g) * sample.A;
        double cp1 = Math.Sqrt(a1 * a1 + reference.B * reference.B);
        double cp2 = Math.Sqrt(a2 * a2 + sample.B * sample.B);
        double h1 = cp1 == 0 ? 0 : Hue(a1, reference.B);
        double h2 = cp2 == 0 ? 0 : Hue(a2, sample.B);
        double dh = h2 - h1;
        if (cp1 * cp2 == 0) dh = 0;
        else if (dh > 180) dh -= 360;
        else if (dh < -180) dh += 360;
        double dl = sample.L - reference.L, dc = cp2 - cp1;
        double dH = 2 * Math.Sqrt(cp1 * cp2) * Sin(dh / 2);
        double meanL = (reference.L + sample.L) / 2, meanC = (cp1 + cp2) / 2;
        double meanH = h1 + h2;
        if (cp1 * cp2 != 0)
        {
            if (Math.Abs(h1 - h2) > 180) meanH += meanH < 360 ? 360 : -360;
            meanH /= 2;
        }
        double t = 1 - 0.17 * Cos(meanH - 30) + 0.24 * Cos(2 * meanH) + 0.32 * Cos(3 * meanH + 6) - 0.20 * Cos(4 * meanH - 63);
        double sl = 1 + 0.015 * Square(meanL - 50) / Math.Sqrt(20 + Square(meanL - 50));
        double sc = 1 + 0.045 * meanC, sh = 1 + 0.015 * meanC * t;
        double c7 = Math.Pow(meanC, 7);
        double rt = -2 * Math.Sqrt(c7 / (c7 + Math.Pow(25, 7))) * Sin(60 * Math.Exp(-Square((meanH - 275) / 25)));
        return Math.Sqrt(Math.Max(0, Square(dl / sl) + Square(dc / sc) + Square(dH / sh) + rt * (dc / sc) * (dH / sh)));
    }

    // CIE94 graphic arts and CMC are asymmetric: the first colour is always the reference.
    public static double DeltaE94(CieLab reference, CieLab sample)
    {
        double c = Chroma(reference), dc = Chroma(sample) - c;
        double dh2 = Math.Max(0, Square(sample.A - reference.A) + Square(sample.B - reference.B) - dc * dc);
        return Math.Sqrt(Square(sample.L - reference.L) + Square(dc / (1 + 0.045 * c)) + dh2 / Square(1 + 0.015 * c));
    }

    public static double DeltaECmc(CieLab reference, CieLab sample, double lightness = 1)
    {
        if (!double.IsFinite(lightness) || lightness <= 0) throw new ArgumentOutOfRangeException(nameof(lightness));
        double c = Chroma(reference), h = Hue(reference.A, reference.B), dc = Chroma(sample) - c;
        double sl = reference.L < 16 ? 0.511 : 0.040975 * reference.L / (1 + 0.01765 * reference.L);
        double sc = 0.0638 * c / (1 + 0.0131 * c) + 0.638;
        double t = h >= 164 && h <= 345 ? 0.56 + Math.Abs(0.2 * Cos(h + 168)) : 0.36 + Math.Abs(0.4 * Cos(h + 35));
        double f = Math.Sqrt(Math.Pow(c, 4) / (Math.Pow(c, 4) + 1900));
        double sh = sc * (f * t + 1 - f);
        double dh2 = Math.Max(0, Square(sample.A - reference.A) + Square(sample.B - reference.B) - dc * dc);
        return Math.Sqrt(Square((sample.L - reference.L) / (lightness * sl)) + Square(dc / sc) + dh2 / (sh * sh));
    }

    public static double Distance(CieChromaticity a, CieChromaticity b) => Math.Sqrt(Square(a.X - b.X) + Square(a.Y - b.Y));

    /// <summary>Nearest projection onto the approximate Planckian locus in CIE 1960 uv, 1667–25000 K.</summary>
    public static CieCctResult EstimateCct(CieChromaticity xy)
    {
        CieChromaticity target = CieColorConverter.XyToCie1960uv(xy);
        if (!target.IsFinite) return CieCctResult.Empty;
        double best = double.MaxValue, kelvin = double.NaN, signed = double.NaN;
        var samples = Planckian.Value;
        for (int i = 0; i < samples.Length - 1; i++)
        {
            double temp = samples[i].Temperature, endTemp = samples[i + 1].Temperature;
            CieChromaticity start = samples[i].Uv, end = samples[i + 1].Uv;
            double dx = end.X - start.X, dy = end.Y - start.Y;
            double fraction = Math.Clamp(((target.X - start.X) * dx + (target.Y - start.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            double ex = target.X - start.X - fraction * dx, ey = target.Y - start.Y - fraction * dy;
            double distance = Math.Sqrt(ex * ex + ey * ey);
            if (distance < best)
            {
                best = distance;
                kelvin = temp + fraction * (endTemp - temp);
                signed = Math.CopySign(distance, dy * ex - dx * ey);
            }
        }
        return best > 0.05 || kelvin <= 1667 || kelvin >= 25000 ? CieCctResult.Empty : new(kelvin, signed);
    }
}
