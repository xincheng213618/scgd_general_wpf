using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace cvColorVision;

/// <summary>Full stored spectrum, before display decimation/clipping. The SDK may already have processed these samples.</summary>
public sealed record SpectrumRawExportRecord
{
    public int MeasurementId { get; init; }
    public DateTime? MeasuredAt { get; init; }
    public string? DeviceCode { get; init; }
    public string Mode { get; init; } = "Luminance";
    public string ModeSource { get; init; } = "ExportContext";
    public string AbsoluteUnit { get; init; } = "W/(sr*m^2*nm)";
    public float StartWavelength { get; init; }
    public float Interval { get; init; }
    public float StoredEndWavelength { get; init; }
    public int StoredArrayLength { get; init; }
    public float AbsoluteScale { get; init; }
    public float[] Samples { get; init; } = Array.Empty<float>();
    public string GridSource { get; init; } = "StoredMetadata";
    public string? AcquisitionParametersJson { get; init; }
    public double? ColorTemperature { get; init; }
    public double? Duv { get; init; }
    public double? Ra { get; init; }
    public double?[] RenderingIndices { get; init; } = new double?[15];
    public string? RenderingStatus { get; init; }

    internal SpectrumRawExportRecord Snapshot() => this with
    {
        Samples = (float[])Samples.Clone(),
        RenderingIndices = (double?[])RenderingIndices.Clone()
    };
}

/// <summary>Invariant, long-form CSV: one row per original valid acquisition sample, including signed/non-finite values.</summary>
public static class SpectrumRawExporter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly string[] Headers = new[]
    {
        "MeasurementId", "MeasuredAt", "DeviceCode", "Mode", "ModeSource", "SampleIndex", "Wavelength(nm)",
        "RelativeSpectrum", "AbsoluteSpectrum", "AbsoluteSpectrumUnit", "StartWavelength(nm)",
        "Interval(nm)", "PointCount", "StoredEndWavelength(nm)", "StoredArrayLength", "AbsoluteScale", "GridSource", "SpectrumSource",
        "AcquisitionParametersJson", "CCT(K)", "Duv", "Ra"
    }.Concat(Enumerable.Range(1, 15).Select(i => $"R{i}")).Append("ColorRenderingStatus").ToArray();

    public static SpectrumRawExportRecord CaptureStoredSpectrum(float[]? samples, float start, float end, float interval, float absoluteScale)
    {
        samples ??= Array.Empty<float>();
        int count = samples.Length;
        bool gridValid = float.IsFinite(start) && start > 0 && float.IsFinite(interval) && interval > 0;
        bool rangeValid = gridValid && float.IsFinite(end) && (end > start || (end == start && count == 1));
        if (rangeValid)
        {
            double requested = Math.Round((end - (double)start) / interval) + 1;
            if (requested >= 1 && requested < count) count = (int)requested;
        }
        return new()
        {
            Samples = samples.Take(count).ToArray(), StartWavelength = start, Interval = interval, AbsoluteScale = absoluteScale,
            StoredEndWavelength = end, StoredArrayLength = samples.Length,
            GridSource = !gridValid ? "InvalidStoredMetadata" : rangeValid ? "StoredMetadata" : "StoredIntervalWithoutValidEnd"
        };
    }

    public static string CreateCsv(IReadOnlyList<SpectrumRawExportRecord> records)
    {
        var snapshot = Capture(records);
        using StringWriter writer = new(Culture);
        Write(writer, snapshot, CancellationToken.None);
        return writer.ToString();
    }

    public static Task WriteAsync(string path, IReadOnlyList<SpectrumRawExportRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var snapshot = Capture(records); // Capture before yielding; later measurements cannot alter the export.
        string destination = Path.GetFullPath(path);
        return Task.Run(() =>
        {
            string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (StreamWriter writer = new(temporary, false, new UTF8Encoding(true))) Write(writer, snapshot, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, destination, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }, cancellationToken);
    }

    private static SpectrumRawExportRecord[] Capture(IReadOnlyList<SpectrumRawExportRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var snapshot = records.Select(record => record.Snapshot()).ToArray();
        foreach (var record in snapshot)
        {
            if (record.Samples.Length == 0)
                throw new ArgumentException($"Measurement {record.MeasurementId}: no stored spectrum.", nameof(records));
        }
        return snapshot;
    }

    private static void Write(TextWriter writer, SpectrumRawExportRecord[] records, CancellationToken cancellationToken)
    {
        WriteRow(writer, Headers);
        foreach (var record in records)
        {
            string? parameters = ParametersForExport(record);
            // Use the round-trip decimal representation of SDK float metadata for nominal wavelengths,
            // avoiding an artificial 780.00000596 nm endpoint from 4000 * (double)0.1f.
            bool gridValid = float.IsFinite(record.StartWavelength) && record.StartWavelength > 0 && float.IsFinite(record.Interval) && record.Interval > 0;
            double start = record.StartWavelength;
            double interval = record.Interval;
            if (gridValid)
            {
                start = double.Parse(record.StartWavelength.ToString("R", Culture), Culture);
                interval = double.Parse(record.Interval.ToString("R", Culture), Culture);
            }
            for (int i = 0; i < record.Samples.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float relative = record.Samples[i];
                var row = new List<string>(Headers.Length)
                {
                    record.MeasurementId.ToString(Culture), record.MeasuredAt?.ToString("O", Culture) ?? "", record.DeviceCode ?? "", record.Mode, record.ModeSource,
                    i.ToString(Culture), gridValid ? (start + i * interval).ToString("0.############", Culture) : "",
                    relative.ToString("R", Culture), ((double)relative * record.AbsoluteScale).ToString("R", Culture), record.AbsoluteUnit,
                    record.StartWavelength.ToString("R", Culture), record.Interval.ToString("R", Culture), record.Samples.Length.ToString(Culture),
                    record.StoredEndWavelength.ToString("R", Culture), record.StoredArrayLength.ToString(Culture),
                    record.AbsoluteScale.ToString("R", Culture), record.GridSource, "StoredSpectrumAfterAcquisitionProcessing",
                    parameters ?? "", Number(record.ColorTemperature), Number(record.Duv), Number(record.Ra)
                };
                row.AddRange(Enumerable.Range(0, 15).Select(index => Number(index < record.RenderingIndices.Length ? record.RenderingIndices[index] : null)));
                row.Add(record.RenderingStatus ?? "");
                WriteRow(writer, row);
            }
        }
    }

    private static readonly HashSet<string> ElectricalParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "Voltage", "Current", "V", "I", "VResult", "IResult", "EqeVoltage", "EqeCurrentMA", "EqeCurrent"
    };

    private static string? ParametersForExport(SpectrumRawExportRecord record)
    {
        string? original = record.AcquisitionParametersJson;
        if (record.Mode != "Luminance" || string.IsNullOrWhiteSpace(original)) return original;
        try
        {
            var parameters = JToken.Parse(original);
            return RemoveElectricalParameters(parameters) ? parameters.ToString(Formatting.None) : original;
        }
        catch (JsonException) { return original; } // Preserve unparseable historical metadata.
    }

    private static bool RemoveElectricalParameters(JToken token)
    {
        bool changed = false;
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties().ToArray())
            {
                if (ElectricalParameters.Contains(property.Name)) { property.Remove(); changed = true; }
                else if (property.Value.Type == JTokenType.String
                    && (property.Name.EndsWith("Json", StringComparison.Ordinal) || property.Name == "StoredParameters"))
                {
                    try
                    {
                        var nested = JToken.Parse(property.Value.Value<string>() ?? "");
                        if (RemoveElectricalParameters(nested)) { property.Value = nested.ToString(Formatting.None); changed = true; }
                    }
                    catch (JsonException) { /* Retain malformed historical parameter strings. */ }
                }
                else changed |= RemoveElectricalParameters(property.Value);
            }
        }
        else if (token is JArray array)
            foreach (var child in array) changed |= RemoveElectricalParameters(child);
        return changed;
    }

    private static string Number(double? value) => value.HasValue && double.IsFinite(value.Value) ? value.Value.ToString("R", Culture) : "";
    private static void WriteRow(TextWriter writer, IEnumerable<string> row)
        => writer.WriteLine(string.Join(",", row.Select(value => value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value)));
}
