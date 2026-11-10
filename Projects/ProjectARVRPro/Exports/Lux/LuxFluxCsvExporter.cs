using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using CsvHelper;
using cvColorVision;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace ProjectARVRPro.Exports.Lux;

internal static class LuxFluxCsvExporter
{
    private const string MissingValue = "-1";
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly int[] Wavelengths = Enumerable.Range(380, 401).ToArray();

    internal static void Export(string path, SpectumResultEntity measurement, ViewResultSpectrum result)
    {
        // The customer parser depends on the existing spelling and order of these columns.
        // Read optional acquisition fields from the record before the view substitutes zeros.
        bool hasChromaticity = measurement.fx.HasValue && measurement.fy.HasValue;
        var chromaticity = hasChromaticity ? result.Chromaticity : new SpectrumChromaticityResult();
        var rendering = result.ColorRendering;
        var fields = new List<(string Header, string Value)>
        {
            ("No", measurement.Id.ToString(Culture)),
            ("Lot", measurement.BatchId?.ToString(Culture) ?? MissingValue),
            ("IP", measurement.fIp is float peakAd && float.IsFinite(peakAd) && !string.IsNullOrWhiteSpace(result.IP) ? result.IP : MissingValue),
            ("EQE(%)", Number(measurement.Eqe * 100)),
            ("LuminousFlux(lm)", Number(measurement.LuminousFlux)),
            ("RadiantFlux(W)", Number(measurement.RadiantFlux)),
            ("LuminousEfficacy(lm/W)", Number(measurement.LuminousEfficacy)),
            ("Cx", Number(measurement.fx)),
            ("Cy", Number(measurement.fy)),
            ("Correlated Color Temperature(CCT)（K）", Number(chromaticity.ColorTemperature)),
            ("Peak Wavelength(λp)(nm)", Number(measurement.fLp)),
            ("Excitation Purity", Number(chromaticity.ExcitationPurity)),
            ("Dominant Wavelength Color", hasChromaticity && !string.IsNullOrWhiteSpace(result.DominantWavelengthHex) ? result.DominantWavelengthHex : MissingValue),
            ("Voltgage(V) (V)", Number(measurement.DataType ? measurement.VResult : null)),
            ("Current(I) (mA)", Number(measurement.DataType ? measurement.IResult : null)),
            ("Color Rendering (Ra)", Number(rendering.Ra)),
            ("Mean Wavelength(nm)", Number(rendering.MeanWavelength)),
        };
        for (int i = 0; i < 15; i++)
            fields.Add(($"R{i + 1}", Number(i < result.RenderingIndices.Length ? result.RenderingIndices[i] : null)));

        fields.AddRange([
            ("Color Rendering Status", rendering.Status.ToString()),
            ("Duv", Number(chromaticity.Duv)),
            ("Complementary Wavelength(nm)", Number(chromaticity.ComplementaryWavelength)),
            ("Wavelength Status", chromaticity.WavelengthStatus.ToString()),
            ("Temperature Status", chromaticity.TemperatureStatus.ToString()),
            ("Purity Reference White", "D65 (0.3127; 0.3290)"),
            ("DW(Ld)(nm)", Number(chromaticity.DominantWavelength)),
            ("Color Purity(%)", Number(chromaticity.ExcitationPurity * 100)),
        ]);

        var samples = Wavelengths.Select(wavelength => ReadSample(result, wavelength)).ToArray();
        for (int i = 0; i < Wavelengths.Length; i++)
            fields.Add((Wavelengths[i].ToString(Culture), Number((double?)(samples[i] * measurement.fPlambda))));
        for (int i = 0; i < Wavelengths.Length; i++)
            fields.Add(($"sp{Wavelengths[i]}", Number(samples[i] is float sample ? Math.Max(sample, 0f) : (double?)null)));

        using var buffer = new StringWriter(Culture);
        using (var csv = new CsvWriter(buffer, Culture))
        {
            foreach (var field in fields) csv.WriteField(field.Header);
            csv.NextRecord();
            foreach (var field in fields) csv.WriteField(field.Value);
            csv.NextRecord();
        }
        File.WriteAllText(path, buffer.ToString(), Encoding.UTF8);
    }

    private static float? ReadSample(ViewResultSpectrum result, int wavelength)
    {
        if (result.fPL is not { Length: > 0 } samples || !float.IsFinite(result.fSpect1)
            || !float.IsFinite(result.fInterval) || result.fInterval <= 0)
            return null;

        double index = Math.Round((wavelength - (double)result.fSpect1) / result.fInterval);
        // Allow stored float grid rounding without shifting a reading to an unmeasured wavelength.
        if (index < 0 || index >= samples.Length || Math.Abs(result.fSpect1 + index * result.fInterval - wavelength) > 0.001)
            return null;
        float sample = samples[(int)index];
        return float.IsFinite(sample) ? sample : null;
    }

    private static string Number<T>(T? value) where T : struct, IFloatingPointIeee754<T> =>
        value is T number && T.IsFinite(number) ? number.ToString(null, Culture) : MissingValue;
}
