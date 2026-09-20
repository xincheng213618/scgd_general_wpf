using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Conoscope.Core
{
    internal sealed record ConoscopeCurveSessionEntry(ConoscopeCurveSnapshot Snapshot, bool IsShown, int ColorIndex);
    internal sealed record ConoscopeCurveSession(IReadOnlyList<ConoscopeCurveSessionEntry> Entries, int SelectedIndex);

    /// <summary>Versioned, source-independent snapshot interchange. Null values represent gaps.</summary>
    internal static class ConoscopeCurveSessionFile
    {
        private const int MaxSamples = 2_000_000;
        private const long MaxFileBytes = 128 * 1024 * 1024;
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

        public static void Save(string path, ConoscopeCurveSession session)
        {
            SessionData data = new()
            {
                Version = 1,
                SelectedIndex = session.SelectedIndex,
                Curves = session.Entries.Select(entry => new CurveData
                {
                    Name = entry.Snapshot.Name, SourceName = entry.Snapshot.SourceName, SourcePath = entry.Snapshot.SourcePath,
                    ModelName = entry.Snapshot.ModelName, CoordinateSystemName = entry.Snapshot.CoordinateSystemName,
                    ReferenceDescription = entry.Snapshot.ReferenceDescription, AxisLabel = entry.Snapshot.AxisLabel,
                    AxisKey = entry.Snapshot.AxisKey, ChannelLabel = entry.Snapshot.ChannelLabel, UnitLabel = entry.Snapshot.UnitLabel,
                    Metadata = entry.Snapshot.Metadata, CapturedAtUtc = entry.Snapshot.CapturedAtUtc,
                    Positions = entry.Snapshot.Positions.ToArray(),
                    Values = entry.Snapshot.Values.Select(value => double.IsFinite(value) ? (double?)value : null).ToArray(),
                    IsShown = entry.IsShown, ColorIndex = entry.ColorIndex
                }).ToArray()
            };
            _ = Validate(data);
            ConoscopeAtomicFile.Write(path, writer =>
            {
                writer.Flush();
                JsonSerializer.Serialize(writer.BaseStream, data, JsonOptions);
                if (writer.BaseStream.Length > MaxFileBytes) throw new InvalidDataException("Curve session exceeds 128 MiB.");
            });
        }

        public static ConoscopeCurveSession Load(string path)
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length > MaxFileBytes) throw new InvalidDataException("Curve session exceeds 128 MiB.");
            // StreamReader also accepts the UTF-8 BOM produced by our atomic writer.
            using StreamReader reader = new(stream);
            SessionData data = JsonSerializer.Deserialize<SessionData>(reader.ReadToEnd(), JsonOptions)
                ?? throw new InvalidDataException("Empty curve session.");
            return Validate(data);
        }

        private static ConoscopeCurveSession Validate(SessionData data)
        {
            if (data.Version != 1) throw new InvalidDataException($"Unsupported curve session version: {data.Version}.");
            if (data.Curves == null || data.SelectedIndex < -1 || data.SelectedIndex >= data.Curves.Length)
                throw new InvalidDataException("Invalid curve selection.");
            List<ConoscopeCurveSessionEntry> entries = new();
            long count = 0;
            foreach (CurveData? curve in data.Curves)
            {
                if (curve == null || curve.Positions == null || curve.Values == null || curve.ColorIndex < 0
                    || curve.Name == null || curve.SourceName == null || curve.SourcePath == null || curve.ModelName == null
                    || curve.CoordinateSystemName == null || curve.ReferenceDescription == null || curve.AxisLabel == null || curve.UnitLabel == null
                    || string.IsNullOrWhiteSpace(curve.AxisKey) || string.IsNullOrWhiteSpace(curve.ChannelLabel)
                    || curve.CapturedAtUtc == default || curve.Values.Any(value => value.HasValue && !double.IsFinite(value.Value)))
                    throw new InvalidDataException("Incomplete curve metadata.");
                count += curve.Positions.Length;
                if (count > MaxSamples) throw new InvalidDataException("A curve session supports at most 2,000,000 samples.");
                ConoscopeCurveSnapshot snapshot = new(curve.Name!, curve.SourceName!, curve.ModelName!, curve.CoordinateSystemName!,
                    curve.ReferenceDescription!, curve.AxisLabel!, curve.ChannelLabel, curve.UnitLabel!, curve.Positions,
                    curve.Values.Select(value => value ?? double.NaN).ToArray(), curve.CapturedAtUtc, curve.SourcePath!, curve.Metadata, curve.AxisKey);
                entries.Add(new(snapshot, curve.IsShown, curve.ColorIndex));
            }
            return new(entries.AsReadOnly(), data.SelectedIndex);
        }

        private sealed class SessionData
        {
            public int Version { get; set; }
            public int SelectedIndex { get; set; }
            public CurveData[]? Curves { get; set; }
        }

        private sealed class CurveData
        {
            public string? Name { get; set; }
            public string? SourceName { get; set; }
            public string? SourcePath { get; set; }
            public string? ModelName { get; set; }
            public string? CoordinateSystemName { get; set; }
            public string? ReferenceDescription { get; set; }
            public string? AxisLabel { get; set; }
            public string? AxisKey { get; set; }
            public string? ChannelLabel { get; set; }
            public string? UnitLabel { get; set; }
            public string? Metadata { get; set; }
            public DateTimeOffset CapturedAtUtc { get; set; }
            public double[]? Positions { get; set; }
            public double?[]? Values { get; set; }
            public bool IsShown { get; set; }
            public int ColorIndex { get; set; }
        }
    }
}
