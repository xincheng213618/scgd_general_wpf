using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Conoscope.Core
{
    /// <summary>A detached copy of the positions and values displayed at capture time.</summary>
    public sealed class ConoscopeCurveSnapshot
    {
        public string Name { get; }
        public string SourceName { get; }
        public string ModelName { get; }
        public string CoordinateSystemName { get; }
        public string ReferenceDescription { get; }
        public string AxisLabel { get; }
        public string AxisKey { get; }
        public string ChannelLabel { get; }
        public string UnitLabel { get; }
        public string? Metadata { get; }
        public DateTimeOffset CapturedAtUtc { get; }
        public IReadOnlyList<double> Positions { get; }
        public IReadOnlyList<double> Values { get; }

        public ConoscopeCurveSnapshot(string name, string sourceName, string modelName, string coordinateSystemName,
            string referenceDescription, string axisLabel, string channelLabel, string unitLabel,
            IReadOnlyList<double> positions, IReadOnlyList<double> values, string? metadata = null, string? axisKey = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(positions);
            ArgumentNullException.ThrowIfNull(values);
            if (positions.Count == 0 || positions.Count != values.Count)
                throw new ArgumentException("A curve snapshot requires a nonempty pair of equally sized sample arrays.");
            double[] copiedPositions = positions.ToArray();
            if (copiedPositions.Any(position => !double.IsFinite(position)))
                throw new ArgumentException("Snapshot positions must be finite.", nameof(positions));

            Name = name.Trim();
            SourceName = sourceName ?? string.Empty;
            ModelName = modelName ?? string.Empty;
            CoordinateSystemName = coordinateSystemName ?? string.Empty;
            ReferenceDescription = referenceDescription ?? string.Empty;
            AxisLabel = axisLabel ?? string.Empty;
            AxisKey = string.IsNullOrWhiteSpace(axisKey) ? AxisLabel : axisKey;
            ChannelLabel = channelLabel ?? string.Empty;
            UnitLabel = unitLabel ?? string.Empty;
            Metadata = metadata;
            CapturedAtUtc = DateTimeOffset.UtcNow;
            Positions = new ReadOnlyCollection<double>(copiedPositions);
            Values = new ReadOnlyCollection<double>(values.ToArray());
        }

        private ConoscopeCurveSnapshot(ConoscopeCurveSnapshot source, string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            Name = name.Trim();
            SourceName = source.SourceName;
            ModelName = source.ModelName;
            CoordinateSystemName = source.CoordinateSystemName;
            ReferenceDescription = source.ReferenceDescription;
            AxisLabel = source.AxisLabel;
            AxisKey = source.AxisKey;
            ChannelLabel = source.ChannelLabel;
            UnitLabel = source.UnitLabel;
            Metadata = source.Metadata;
            CapturedAtUtc = source.CapturedAtUtc;
            Positions = source.Positions;
            Values = source.Values;
        }

        public ConoscopeCurveSnapshot WithName(string name) => new(this, name);

        public bool IsCompatibleWith(ConoscopeCurveSnapshot other)
        {
            ArgumentNullException.ThrowIfNull(other);
            return string.Equals(AxisKey, other.AxisKey, StringComparison.Ordinal)
                && string.Equals(UnitLabel, other.UnitLabel, StringComparison.Ordinal);
        }

        /// <summary>Writes the captured samples without interpolation, rounding, or source-image access.</summary>
        public void WriteCsv(TextWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);
            WriteRow(writer, "# Name", Name);
            WriteRow(writer, "# Source", SourceName);
            WriteRow(writer, "# Model", ModelName);
            WriteRow(writer, "# CoordinateSystem", CoordinateSystemName);
            WriteRow(writer, "# Reference", ReferenceDescription);
            WriteRow(writer, "# AxisKey", AxisKey);
            WriteRow(writer, "# AxisLabel", AxisLabel);
            WriteRow(writer, "# Channel", ChannelLabel);
            WriteRow(writer, "# Unit", UnitLabel);
            WriteRow(writer, "# CapturedAtUtc", CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            WriteRow(writer, "# SampleCount", Positions.Count.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(Metadata)) WriteRow(writer, "# Metadata", Metadata);
            WriteRow(writer, "Position", "Value");
            for (int index = 0; index < Positions.Count; index++)
                WriteRow(writer, Positions[index].ToString("R", CultureInfo.InvariantCulture), Values[index].ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteRow(TextWriter writer, string first, string second)
            => writer.WriteLine($"{EscapeCsv(first)},{EscapeCsv(second)}");

        private static string EscapeCsv(string value)
            => value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
