using System;
using System.Linq;

namespace cvColorVision;

public enum SpectrumWavelengthStatus { Valid, Achromatic, Complementary, InvalidChromaticity, OutsideLocus, InvalidReferenceWhite }
public enum SpectrumTemperatureStatus { Valid, InvalidChromaticity, OutOfRange, FarFromPlanckianLocus }

public sealed class SpectrumChromaticityResult
{
    public SpectrumTemperatureStatus TemperatureStatus { get; internal set; } = SpectrumTemperatureStatus.InvalidChromaticity;
    public SpectrumWavelengthStatus WavelengthStatus { get; internal set; } = SpectrumWavelengthStatus.InvalidChromaticity;
    public double? ColorTemperature { get; internal set; }
    /// <summary>Signed distance to the Planckian locus in CIE 1960 UCS, not Δu'v' or CRI reference distance.</summary>
    public double? Duv { get; internal set; }
    public double? DominantWavelength { get; internal set; }
    public double? ComplementaryWavelength { get; internal set; }
    public double? ExcitationPurity { get; internal set; }
}

/// <summary>CIE 1931 2° xy colourimetry. Wavelength/purity use D65 by default and the 380–780 nm locus.</summary>
public static class SpectrumChromaticity
{
    public const double ReferenceX = 0.3127;
    public const double ReferenceY = 0.3290;
    // Match Colour plot_single_sd: CIE 1931 2°, E -> D65 CAT02, then a single
    // linear-RGB maximum over 380–780 nm before clipping and sRGB encoding.
    // Keep that domain fixed so zooming or measuring a narrower band never recolours a wavelength.
    private static readonly double DisplayMaximum = Enumerable.Range(380, 401).Select(w => DisplayLinearRgb(w))
        .Max(rgb => Math.Max(rgb.red, Math.Max(rgb.green, rgb.blue)));

    /// <summary>Illustrative sRGB matching Colour's visible-spectrum plotting convention. Not a display calibration.</summary>
    public static (byte red, byte green, byte blue) DisplayRgb(double wavelength)
    {
        if (!double.IsFinite(wavelength) || wavelength < 380 || wavelength > 780) return (0, 0, 0);
        var rgb = DisplayLinearRgb(wavelength);
        static byte Encode(double value)
        {
            value = Math.Clamp(value, 0, 1);
            return (byte)Math.Round(255 * (value <= 0.0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1 / 2.4) - 0.055));
        }
        return (Encode(rgb.red / DisplayMaximum), Encode(rgb.green / DisplayMaximum), Encode(rgb.blue / DisplayMaximum));
    }

    private static (double red, double green, double blue) DisplayLinearRgb(double wavelength)
    {
        double[] xyz = SpectrumColorRendering.InterpolateTable(wavelength);
        // sRGB XYZ-to-RGB matrix multiplied by the CAT02 E-to-D65 adaptation matrix.
        return (3.174821072200531 * xyz[0] - 1.6974555545245795 * xyz[1] - 0.4775222350011802 * xyz[2],
            -0.9899325492487888 * xyz[0] + 1.9499869504250902 * xyz[1] + 0.040044747759869256 * xyz[2],
            0.06047909499102054 * xyz[0] - 0.2089180222454224 * xyz[1] + 1.1485133649443713 * xyz[2]);
    }
    private static readonly (double x, double y)[] Locus = Enumerable.Range(380, 401).Select(wavelength =>
    {
        double[] row = SpectrumColorRendering.InterpolateTable(wavelength);
        double sum = row[0] + row[1] + row[2];
        return (row[0] / sum, row[1] / sum);
    }).ToArray();

    public static SpectrumChromaticityResult Calculate(double x, double y, double referenceX = ReferenceX, double referenceY = ReferenceY)
    {
        SpectrumChromaticityResult result = new();
        if (!ValidXy(x, y)) return result;
        double denominator = -2 * x + 12 * y + 3;
        var temperature = Robertson(4 * x / denominator, 6 * y / denominator);
        result.Duv = double.IsFinite(temperature.duv) ? temperature.duv : null;
        result.TemperatureStatus = !double.IsFinite(temperature.temperature) || temperature.temperature < 1667 || temperature.temperature > 25000
            ? SpectrumTemperatureStatus.OutOfRange
            : Math.Abs(temperature.duv) > 0.05 ? SpectrumTemperatureStatus.FarFromPlanckianLocus : SpectrumTemperatureStatus.Valid;
        if (result.TemperatureStatus == SpectrumTemperatureStatus.Valid) result.ColorTemperature = temperature.temperature;
        if (result.TemperatureStatus == SpectrumTemperatureStatus.OutOfRange) result.Duv = null;

        if (!ValidXy(referenceX, referenceY) || !Inside(referenceX, referenceY))
        {
            result.WavelengthStatus = SpectrumWavelengthStatus.InvalidReferenceWhite;
            return result;
        }
        double dx = x - referenceX, dy = y - referenceY;
        // Float acquisition coordinates do not distinguish points closer than this.
        if (dx * dx + dy * dy <= 1e-14)
        {
            result.WavelengthStatus = SpectrumWavelengthStatus.Achromatic;
            result.ExcitationPurity = 0;
            return result;
        }
        var hit = Intersect(referenceX, referenceY, dx, dy);
        if (!double.IsFinite(hit.t) || hit.t < 1 - 1e-7)
        {
            result.WavelengthStatus = SpectrumWavelengthStatus.OutsideLocus;
            return result;
        }
        result.ExcitationPurity = Math.Min(1, 1 / hit.t);
        if (hit.edge == Locus.Length - 1)
        {
            var opposite = Intersect(referenceX, referenceY, -dx, -dy);
            result.WavelengthStatus = SpectrumWavelengthStatus.Complementary;
            result.ComplementaryWavelength = 380 + opposite.edge + opposite.fraction;
        }
        else
        {
            result.WavelengthStatus = SpectrumWavelengthStatus.Valid;
            result.DominantWavelength = 380 + hit.edge + hit.fraction;
        }
        return result;
    }

    internal static (double temperature, double duv) Robertson(double u, double v)
    {
        double[][] lines = SpectrumColorRendering.Isotherms;
        double previousDistance = 0, previousDu = 0, previousDv = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            double[] line = lines[i];
            double du = 1 / Math.Sqrt(1 + line[3] * line[3]), dv = line[3] * du;
            double distance = -(u - line[1]) * dv + (v - line[2]) * du;
            if (distance <= 0 || i == lines.Length - 1)
            {
                double current = -Math.Min(distance, 0);
                double fraction = i == 1 ? 0 : current / (previousDistance + current);
                double temperature = 1e6 / (lines[i - 1][0] * fraction + line[0] * (1 - fraction));
                double u0 = lines[i - 1][1] * fraction + line[1] * (1 - fraction);
                double v0 = lines[i - 1][2] * fraction + line[2] * (1 - fraction);
                du = du * (1 - fraction) + previousDu * fraction;
                dv = dv * (1 - fraction) + previousDv * fraction;
                double duv = -((u - u0) * du + (v - v0) * dv) / Math.Sqrt(du * du + dv * dv);
                return (temperature, duv);
            }
            previousDistance = distance;
            previousDu = du;
            previousDv = dv;
        }
        return (double.NaN, double.NaN);
    }

    private static bool ValidXy(double x, double y) => double.IsFinite(x) && double.IsFinite(y) && x >= 0 && y > 0 && x + y <= 1.0000001;
    private static double Cross(double x, double y, double a, double b) => x * b - y * a;
    private static (double t, int edge, double fraction) Intersect(double x, double y, double dx, double dy)
    {
        double best = double.PositiveInfinity, fraction = 0;
        int edge = -1;
        for (int i = 0; i < Locus.Length; i++)
        {
            var a = Locus[i]; var b = Locus[(i + 1) % Locus.Length];
            double ex = b.x - a.x, ey = b.y - a.y;
            double denominator = Cross(dx, dy, ex, ey);
            if (Math.Abs(denominator) < 1e-18) continue;
            double t = Cross(a.x - x, a.y - y, ex, ey) / denominator;
            double s = Cross(a.x - x, a.y - y, dx, dy) / denominator;
            // At the shared violet/red endpoints, prefer the spectral edge over
            // the purple line even when floating-point roundoff makes it nearer.
            if (i == Locus.Length - 1 && edge >= 0 && Math.Abs(t - best) <= 1e-9 * Math.Max(1, best)) continue;
            if (t > 1e-10 && t < best && s >= -1e-9 && s <= 1 + 1e-9)
            {
                best = t; edge = i; fraction = Math.Clamp(s, 0, 1);
            }
        }
        return (best, edge, fraction);
    }
    private static bool Inside(double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = Locus.Length - 1; i < Locus.Length; j = i++)
            if ((Locus[i].y > y) != (Locus[j].y > y) && x < (Locus[j].x - Locus[i].x) * (y - Locus[i].y) / (Locus[j].y - Locus[i].y) + Locus[i].x)
                inside = !inside;
        return inside;
    }
}
