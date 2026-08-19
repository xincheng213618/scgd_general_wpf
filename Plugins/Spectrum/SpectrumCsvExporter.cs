using Spectrum.Models;
using System.Globalization;
using System.IO;
using System.Text;

namespace Spectrum;

public static class SpectrumCsvExporter
{
    private static readonly CultureInfo CsvCulture = CultureInfo.InvariantCulture;
    private static readonly Encoding CsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private static readonly CsvColumn[] NormalColumns =
    [
        new("No", result => result.Id),
        new("IP", result => result.IP),
        new("Luminance(Lv)(cd/m2)", result => result.Lv),
        new("Blue Light Intensity", result => result.Blue),
        new("CIEx", result => result.fCIEx),
        new("CIEy", result => result.fCIEy),
        new("CIEz", result => result.fCIEz),
        new("Cx", result => result.fx),
        new("Cy", result => result.fy),
        new("u'", result => result.fu),
        new("v'", result => result.fv),
        new("Correlated Color Temperature(CCT)(K)", result => result.fCCT),
        new("DW(Ld)(nm)", result => result.fLd),
        new("Color Purity(%)", result => result.ColorPurityPercent),
        new("Peak Wavelength(Lp)(nm)", result => result.fLp),
        new("Color Rendering(Ra)", result => result.fRa),
        new("FWHM", result => result.fHW),
        new("Excitation Purity(%)", result => result.ExcitationPurityPercent),
        new("CIE2015X", result => result.fCIEx2015),
        new("CIE2015Y", result => result.fCIEy2015),
        new("CIE2015Z", result => result.fCIEz2015),
        new("CIE2015x", result => result.fx2015),
        new("CIE2015y", result => result.fy2015),
        new("CIE2015u", result => result.fu2015),
        new("CIE2015v", result => result.fv2015)
    ];

    private static readonly CsvColumn[] EqeColumns =
    [
        new("No", result => result.Id),
        new("IP", result => result.IP),
        new("EQE(%)", result => result.EqePercent),
        new("LuminousFlux(lm)", result => result.LuminousFlux),
        new("RadiantFlux(W)", result => result.RadiantFlux),
        new("LuminousEfficacy(lm/W)", result => result.LuminousEfficacy),
        new("Cx", result => result.fx),
        new("Cy", result => result.fy),
        new("Correlated Color Temperature(CCT)(K)", result => result.fCCT),
        new("Peak Wavelength(Lp)(nm)", result => result.fLp),
        new("Excitation Purity(%)", result => result.ExcitationPurityPercent),
        new("Voltage(V)", result => result.V),
        new("Current(mA)", result => result.I),
        new("CIE2015X", result => result.fCIEx2015),
        new("CIE2015Y", result => result.fCIEy2015),
        new("CIE2015Z", result => result.fCIEz2015),
        new("CIE2015x", result => result.fx2015),
        new("CIE2015y", result => result.fy2015),
        new("CIE2015u", result => result.fu2015),
        new("CIE2015v", result => result.fv2015)
    ];

    public static string CreateCsv(IReadOnlyList<ViewResultSpectrum> results, bool isEqeMode)
    {
        ArgumentNullException.ThrowIfNull(results);
        CsvExportSnapshot snapshot = Capture(results, isEqeMode, CancellationToken.None);
        StringBuilder builder = new();
        foreach (string row in CreateRows(snapshot))
            builder.AppendLine(row);

        return builder.ToString();
    }

    public static async Task WriteAsync(
        string filePath,
        IReadOnlyList<ViewResultSpectrum> results,
        bool isEqeMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(results);
        CsvExportSnapshot snapshot = Capture(results, isEqeMode, cancellationToken);
        await Task.Run(() =>
        {
            using StreamWriter writer = new(filePath, append: false, CsvEncoding);
            foreach (string row in CreateRows(snapshot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteLine(row);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static CsvExportSnapshot Capture(
        IReadOnlyList<ViewResultSpectrum> results,
        bool isEqeMode,
        CancellationToken cancellationToken)
    {
        CsvColumn[] resultColumns = isEqeMode ? EqeColumns : NormalColumns;
        CsvResultSnapshot[] resultSnapshots = new CsvResultSnapshot[results.Count];
        HashSet<double> wavelengths = [];

        for (int index = 0; index < results.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ViewResultSpectrum result = results[index];
            CsvSampleSnapshot[] samples = result.SpectralDatas
                .Select(sample => new CsvSampleSnapshot(
                    NormalizeWavelength(sample.Wavelength),
                    sample.AbsoluteSpectrum,
                    sample.RelativeSpectrum))
                .Where(sample => double.IsFinite(sample.Wavelength))
                .ToArray();
            foreach (CsvSampleSnapshot sample in samples)
                wavelengths.Add(sample.Wavelength);

            resultSnapshots[index] = new CsvResultSnapshot(
                resultColumns.Select(column => column.GetValue(result)).ToArray(),
                samples);
        }

        return new CsvExportSnapshot(
            resultColumns.Select(column => column.Header).ToArray(),
            wavelengths.Order().ToArray(),
            resultSnapshots);
    }

    private static IEnumerable<string> CreateRows(CsvExportSnapshot snapshot)
    {
        List<object?> header = new(snapshot.Headers.Length + snapshot.Wavelengths.Length * 2);
        header.AddRange(snapshot.Headers);
        header.AddRange(snapshot.Wavelengths.Select(FormatWavelength));
        header.AddRange(snapshot.Wavelengths.Select(wavelength => $"sp{FormatWavelength(wavelength)}"));
        yield return CreateRow(header);

        foreach (CsvResultSnapshot result in snapshot.Results)
        {
            Dictionary<double, CsvSampleSnapshot> samplesByWavelength = [];
            foreach (CsvSampleSnapshot sample in result.Samples)
                samplesByWavelength.TryAdd(sample.Wavelength, sample);

            List<object?> row = new(result.Values.Length + snapshot.Wavelengths.Length * 2);
            row.AddRange(result.Values);
            foreach (double wavelength in snapshot.Wavelengths)
                row.Add(samplesByWavelength.TryGetValue(wavelength, out CsvSampleSnapshot? sample) ? sample.AbsoluteSpectrum : null);
            foreach (double wavelength in snapshot.Wavelengths)
                row.Add(samplesByWavelength.TryGetValue(wavelength, out CsvSampleSnapshot? sample) ? sample.RelativeSpectrum : null);
            yield return CreateRow(row);
        }
    }

    private static double NormalizeWavelength(double wavelength) => Math.Round(wavelength, 6, MidpointRounding.AwayFromZero);

    private static string FormatWavelength(double wavelength) => wavelength.ToString("0.######", CsvCulture);

    private static string CreateRow(IEnumerable<object?> values)
    {
        StringBuilder builder = new();
        bool isFirst = true;
        foreach (object? value in values)
        {
            if (!isFirst)
                builder.Append(',');
            builder.Append(Escape(FormatValue(value)));
            isFirst = false;
        }
        return builder.ToString();
    }

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        IFormattable formattable => formattable.ToString(null, CsvCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private sealed record CsvColumn(string Header, Func<ViewResultSpectrum, object?> GetValue);
    private sealed record CsvExportSnapshot(string[] Headers, double[] Wavelengths, CsvResultSnapshot[] Results);
    private sealed record CsvResultSnapshot(object?[] Values, CsvSampleSnapshot[] Samples);
    private sealed record CsvSampleSnapshot(double Wavelength, double AbsoluteSpectrum, double RelativeSpectrum);
}
