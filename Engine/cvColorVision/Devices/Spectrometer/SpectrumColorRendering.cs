using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace cvColorVision;

public enum ColorRenderingStatus
{
    Valid,
    InvalidSpectrum,
    InsufficientWavelengthRange,
    SamplingTooCoarse,
    UnsupportedColorTemperature,
    FarFromReference
}

/// <summary>CIE 13.3-1995 R1–R14 and the optional Japanese complexion sample R15.</summary>
public sealed class SpectrumColorRenderingResult
{
    public ColorRenderingStatus Status { get; internal set; } = ColorRenderingStatus.InvalidSpectrum;
    public double? MeanWavelength { get; internal set; }
    public double? ColorTemperature { get; internal set; }
    public double? Duv { get; internal set; }
    public double? ReferenceDistance { get; internal set; }
    public double? Ra { get; internal set; }
    public double[] Ri { get; internal set; } = Array.Empty<double>();
}

/// <summary>
/// Spectral centroid and CIE 13.3 test-colour method: Robertson CCT, Planck/daylight
/// reference, von Kries adaptation, CIE 1964 U*V*W*, Ri = 100 - 4.6 ΔE.
/// Tables and provenance are in ColorRenderingData. Only the measured 380–780 nm
/// range is used, without extrapolation. Negative/non-finite spectra are rejected.
/// </summary>
public static class SpectrumColorRendering
{
    private static readonly double[][] Tables = LoadTable("cie-cri-tables.csv");
    internal static readonly double[][] Isotherms = LoadTable("robertson-1968.csv");

    public static SpectrumColorRenderingResult Calculate(float[] spectrum, double start, double interval)
    {
        SpectrumColorRenderingResult result = new() { Status = ColorRenderingStatus.InvalidSpectrum };
        if (spectrum == null || spectrum.Length < 2 || !double.IsFinite(start) || start <= 0
            || !double.IsFinite(interval) || interval <= 0)
            return result;

        double end = start + (spectrum.Length - 1) * interval;
        if (!double.IsFinite(end)) return result;
        double maximum = 0;
        foreach (float value in spectrum)
        {
            if (!float.IsFinite(value) || value < 0) return result;
            maximum = Math.Max(maximum, value);
        }
        if (maximum <= 0) return result;

        // Trapezoidal spectral radiant-power centroid over the measured range.
        double energy = 0, moment = 0;
        for (int i = 0; i < spectrum.Length; i++)
        {
            double weight = i == 0 || i == spectrum.Length - 1 ? 0.5 : 1;
            double signal = spectrum[i] / maximum * weight;
            energy += signal;
            moment += (start + i * interval) * signal;
        }
        if (energy <= 0) return result;
        result.MeanWavelength = moment / energy;

        // Accommodate float metadata (e.g. 4000 * 0.1f); do not infer missing bands.
        if (start > 380.0001 || end < 779.9999)
        {
            result.Status = ColorRenderingStatus.InsufficientWavelengthRange;
            return result;
        }
        if (interval > 5)
        {
            result.Status = ColorRenderingStatus.SamplingTooCoarse;
            return result;
        }

        // Keep fine native sampling. Interpolate CMFs/TCS, not narrow emission peaks.
        int steps = (int)Math.Ceiling(400 / Math.Max(interval, 0.1));
        double step = 400d / steps;
        int count = steps + 1;
        double[] wavelengths = new double[count];
        double[] test = new double[count];
        double[][] data = new double[count][];
        for (int i = 0; i < count; i++)
        {
            double wavelength = 380 + i * step;
            wavelengths[i] = wavelength;
            test[i] = InterpolateSpectrum(spectrum, (wavelength - start) / interval) / maximum;
            data[i] = InterpolateTable(wavelength);
        }

        var white = Integrate(test, data, -1);
        var uv = ToUv(white);
        if (!double.IsFinite(uv.u) || !double.IsFinite(uv.v)) return result;
        var correlated = SpectrumChromaticity.Robertson(uv.u, uv.v);
        double temperature = correlated.temperature;
        result.Duv = double.IsFinite(correlated.duv) ? correlated.duv : null;
        result.ColorTemperature = double.IsFinite(temperature) ? temperature : null;
        if (!double.IsFinite(temperature) || temperature < 1667 || temperature > 25000)
        {
            result.Status = ColorRenderingStatus.UnsupportedColorTemperature;
            return result;
        }

        double[] reference = CreateReference(wavelengths, data, temperature);
        var referenceWhite = Integrate(reference, data, -1);
        var referenceUv = ToUv(referenceWhite);
        double distance = Math.Sqrt(Math.Pow(uv.u - referenceUv.u, 2) + Math.Pow(uv.v - referenceUv.v, 2));
        result.ReferenceDistance = distance;
        // CIE 13.3 chromaticity agreement requirement; an invalid CRI is not zero.
        if (distance > 0.0054)
        {
            result.Status = ColorRenderingStatus.FarFromReference;
            return result;
        }

        double[] ri = new double[15];
        for (int sample = 0; sample < ri.Length; sample++)
        {
            var testSample = Integrate(test, data, sample);
            var referenceSample = Integrate(reference, data, sample);
            var testUv = ToUv(testSample);
            var sampleRefUv = ToUv(referenceSample);
            var adapted = Adapt(testUv.u, testUv.v, uv.u, uv.v, referenceUv.u, referenceUv.v);
            double wt = 25 * Math.Cbrt(100 * testSample.y / white.y) - 17;
            double wr = 25 * Math.Cbrt(100 * referenceSample.y / referenceWhite.y) - 17;
            double du = 13 * (wt * (adapted.u - referenceUv.u) - wr * (sampleRefUv.u - referenceUv.u));
            double dv = 13 * (wt * (adapted.v - referenceUv.v) - wr * (sampleRefUv.v - referenceUv.v));
            ri[sample] = 100 - 4.6 * Math.Sqrt(du * du + dv * dv + (wt - wr) * (wt - wr));
        }
        if (ri.Any(value => !double.IsFinite(value))) return result;
        result.Ri = ri;
        result.Ra = ri.Take(8).Average(); // Do not clamp negative special indices (e.g. R9).
        result.Status = ColorRenderingStatus.Valid;
        return result;
    }

    /// <summary>Fill existing ABI fields before managed persistence/export; ABI layout stays unchanged.</summary>
    public static COLOR_PARA Apply(COLOR_PARA value)
    {
        float[] spectrum = value.fPL ?? Array.Empty<float>();
        if (float.IsFinite(value.fInterval) && value.fInterval > 0 && value.fSpect2 > value.fSpect1)
        {
            double count = Math.Round((value.fSpect2 - value.fSpect1) / (double)value.fInterval) + 1;
            if (count > 0 && count < spectrum.Length) spectrum = spectrum.Take((int)count).ToArray();
        }
        SpectrumColorRenderingResult result = Calculate(spectrum, value.fSpect1, value.fInterval);
        value.fLav = (float)(result.MeanWavelength ?? double.NaN);
        value.fRa = (float)(result.Ra ?? double.NaN);
        value.fRi = result.Ri.Length == 15 ? result.Ri.Select(index => (float)index).ToArray() : Enumerable.Repeat(float.NaN, 15).ToArray();
        SpectrumChromaticityResult chromaticity = SpectrumChromaticity.Calculate(value.fx, value.fy);
        value.fCCT = (float)(chromaticity.ColorTemperature ?? double.NaN);
        value.dC = (float)(chromaticity.Duv ?? double.NaN);
        value.fLd = (float)(chromaticity.DominantWavelength ?? -chromaticity.ComplementaryWavelength ?? double.NaN);
        value.fPur = (float)(chromaticity.ExcitationPurity ?? double.NaN);
        return value;
    }

    private static double InterpolateSpectrum(float[] values, double position)
    {
        position = Math.Clamp(position, 0, values.Length - 1);
        int left = (int)position;
        return values[left] + (values[Math.Min(left + 1, values.Length - 1)] - (double)values[left]) * (position - left);
    }

    internal static double[] InterpolateTable(double wavelength)
    {
        double position = wavelength - 380;
        int left = Math.Clamp((int)position, 0, 400);
        double[] row = new double[21];
        for (int column = 0; column < row.Length; column++)
            row[column] = Tables[left][column + 1] + (Tables[Math.Min(left + 1, 400)][column + 1] - Tables[left][column + 1]) * (position - left);
        return row;
    }

    private static (double x, double y, double z) Integrate(double[] spectrum, double[][] table, int sample)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < spectrum.Length; i++)
        {
            double value = spectrum[i] * (sample < 0 ? 1 : table[i][6 + sample]);
            x += value * table[i][0];
            y += value * table[i][1];
            z += value * table[i][2];
        }
        return (x, y, z); // Uniform integration interval cancels in ratios.
    }

    private static (double u, double v) ToUv((double x, double y, double z) xyz)
    {
        double denominator = xyz.x + 15 * xyz.y + 3 * xyz.z;
        return (4 * xyz.x / denominator, 6 * xyz.y / denominator);
    }

    private static double[] CreateReference(double[] wavelengths, double[][] data, double temperature)
    {
        double[] values = new double[wavelengths.Length];
        if (temperature < 5000)
        {
            for (int i = 0; i < values.Length; i++)
            {
                double lambda = wavelengths[i] * 1e-9;
                values[i] = 1 / (Math.Pow(lambda, 5) * (Math.Exp(0.014388 / (lambda * temperature)) - 1));
            }
        }
        else
        {
            double t2 = temperature * temperature, t3 = t2 * temperature;
            double x = temperature <= 7000
                ? -4.607e9 / t3 + 2.9678e6 / t2 + 99.11 / temperature + 0.244063
                : -2.0064e9 / t3 + 1.9018e6 / t2 + 247.48 / temperature + 0.23704;
            double y = -3 * x * x + 2.87 * x - 0.275;
            double denominator = 0.0241 + 0.2562 * x - 0.7341 * y;
            double m1 = Math.Round((-1.3515 - 1.7703 * x + 5.9114 * y) / denominator, 3);
            double m2 = Math.Round((0.03 - 31.4424 * x + 30.0717 * y) / denominator, 3);
            for (int i = 0; i < values.Length; i++)
                values[i] = data[i][3] + m1 * data[i][4] + m2 * data[i][5];
        }
        double maximum = values.Max();
        for (int i = 0; i < values.Length; i++) values[i] /= maximum;
        return values;
    }

    private static (double u, double v) Adapt(double u, double v, double ut, double vt, double ur, double vr)
    {
        static double C(double a, double b) => (4 - a - 10 * b) / b;
        static double D(double a, double b) => (1.708 * b + 0.404 - 1.481 * a) / b;
        double c = C(ur, vr) / C(ut, vt) * C(u, v);
        double d = D(ur, vr) / D(ut, vt) * D(u, v);
        double denominator = 16.518 + 1.481 * c - d;
        return ((10.872 + 0.404 * c - 4 * d) / denominator, 5.52 / denominator);
    }

    private static double[][] LoadTable(string name)
    {
        using Stream stream = typeof(SpectrumColorRendering).Assembly.GetManifestResourceStream("cvColorVision.ColorRenderingData." + name)
            ?? throw new InvalidOperationException("Missing CRI data: " + name);
        using StreamReader reader = new(stream);
        reader.ReadLine();
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray()).ToArray();
    }
}
