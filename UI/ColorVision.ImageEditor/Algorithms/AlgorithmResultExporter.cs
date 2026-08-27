using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Exports host-neutral analysis artifacts without introducing file-system concerns into the contract assembly.</summary>
    public static class AlgorithmResultExporter
    {
        public static string ExportJson(AlgorithmResult result, string filePath, bool overwrite = false)
        {
            ArgumentNullException.ThrowIfNull(result);
            string target = PrepareTarget(filePath, overwrite);
            string json = JsonSerializer.Serialize(new
            {
                result.InvocationId,
                algorithmId = result.AlgorithmId.Value,
                algorithmVersion = result.AlgorithmVersion.ToString(),
                status = result.Status.ToString(),
                result.Artifacts,
                result.Failures,
                result.Diagnostics,
            }, AlgorithmJson.Options);
            WriteAtomically(target, json, overwrite);
            return target;
        }

        /// <summary>
        /// Writes measurements to the selected CSV path and every Table/Geometry/StructuredData artifact to an adjacent suffixed CSV.
        /// All target names are reserved before the first write, so overwrite refusal is deterministic.
        /// </summary>
        public static IReadOnlyList<string> ExportCsvBundle(AlgorithmResult result, string filePath, bool overwrite = false)
        {
            ArgumentNullException.ThrowIfNull(result);
            string primary = Path.GetFullPath(filePath);
            string directory = Path.GetDirectoryName(primary) ?? throw new ArgumentException("The export path has no directory.", nameof(filePath));
            string stem = Path.GetFileNameWithoutExtension(primary);
            if (string.IsNullOrWhiteSpace(stem)) throw new ArgumentException("The export path has no file name.", nameof(filePath));
            Directory.CreateDirectory(directory);

            List<(string Path, string Content)> outputs = new();
            AlgorithmMeasurementArtifact[] measurements = result.Artifacts.OfType<AlgorithmMeasurementArtifact>().ToArray();
            outputs.Add((primary, MeasurementsCsv(measurements)));
            foreach (AlgorithmTableArtifact table in result.Artifacts.OfType<AlgorithmTableArtifact>())
                outputs.Add((Adjacent(directory, stem, table.Name), TableCsv(table)));
            foreach (AlgorithmGeometryArtifact geometry in result.Artifacts.OfType<AlgorithmGeometryArtifact>())
                outputs.Add((Adjacent(directory, stem, geometry.Name), GeometryCsv(geometry)));
            foreach (AlgorithmStructuredDataArtifact structured in result.Artifacts.OfType<AlgorithmStructuredDataArtifact>())
                outputs.Add((Adjacent(directory, stem, structured.Name), StructuredCsv(structured)));

            string[] duplicate = outputs.GroupBy(output => output.Path, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicate.Length > 0) throw new InvalidOperationException($"Artifact export paths collide: {string.Join(", ", duplicate)}");
            if (!overwrite)
            {
                string? existing = outputs.Select(output => output.Path).FirstOrDefault(File.Exists);
                if (existing != null) throw new IOException($"The export target already exists: {existing}");
            }

            List<(string Temporary, string Target)> staged = new();
            List<string> committed = new();
            try
            {
                foreach ((string target, string content) in outputs)
                {
                    string temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    staged.Add((temporary, target));
                }
                foreach ((string temporary, string target) in staged)
                {
                    File.Move(temporary, target, overwrite);
                    committed.Add(target);
                }
                return outputs.Select(output => output.Path).ToArray();
            }
            catch
            {
                foreach ((string temporary, _) in staged)
                {
                    try { if (File.Exists(temporary)) File.Delete(temporary); }
                    catch { }
                }
                if (!overwrite)
                {
                    foreach (string target in committed)
                    {
                        try { if (File.Exists(target)) File.Delete(target); }
                        catch { }
                    }
                }
                throw;
            }
        }

        private static string MeasurementsCsv(IEnumerable<AlgorithmMeasurementArtifact> artifacts)
        {
            StringBuilder csv = new();
            AppendRow(csv, "Artifact", "Name", "Value", "Unit", "Channel", "Confidence", "QualifiersJson");
            foreach (AlgorithmMeasurementArtifact artifact in artifacts)
            {
                foreach (AlgorithmMeasurement measurement in artifact.Measurements)
                {
                    AppendRow(csv,
                        artifact.Name,
                        measurement.Name,
                        measurement.Value.ToString("R", CultureInfo.InvariantCulture),
                        measurement.Unit,
                        measurement.Channel?.ToString(CultureInfo.InvariantCulture),
                        measurement.Confidence?.ToString("R", CultureInfo.InvariantCulture),
                        measurement.Qualifiers == null ? null : JsonSerializer.Serialize(measurement.Qualifiers, AlgorithmJson.Options));
                }
            }
            return csv.ToString();
        }

        private static string TableCsv(AlgorithmTableArtifact table)
        {
            StringBuilder csv = new();
            AppendRow(csv, table.Columns.Select(column => column.Name).ToArray());
            foreach (IReadOnlyDictionary<string, JsonElement> row in table.Rows)
            {
                AppendRow(csv, table.Columns.Select(column => row.TryGetValue(column.Name, out JsonElement value) ? JsonText(value) : null).ToArray());
            }
            return csv.ToString();
        }

        private static string GeometryCsv(AlgorithmGeometryArtifact artifact)
        {
            StringBuilder csv = new();
            AppendRow(csv, "Artifact", "CoordinateSpace", "Id", "Kind", "PointsJson", "Radius", "MatrixJson", "Residual", "Confidence", "FilterReason", "MeasurementsJson");
            foreach (AlgorithmGeometry geometry in artifact.Geometries)
            {
                AppendRow(csv,
                    artifact.Name,
                    artifact.CoordinateSpace.ToString(),
                    geometry.Id,
                    geometry.Kind.ToString(),
                    JsonSerializer.Serialize(geometry.Points, AlgorithmJson.Options),
                    geometry.Radius?.ToString("R", CultureInfo.InvariantCulture),
                    geometry.Matrix == null ? null : JsonSerializer.Serialize(geometry.Matrix, AlgorithmJson.Options),
                    geometry.Residual?.ToString("R", CultureInfo.InvariantCulture),
                    geometry.Confidence?.ToString("R", CultureInfo.InvariantCulture),
                    geometry.FilterReason,
                    geometry.Measurements == null ? null : JsonSerializer.Serialize(geometry.Measurements, AlgorithmJson.Options));
            }
            return csv.ToString();
        }

        private static string StructuredCsv(AlgorithmStructuredDataArtifact artifact)
        {
            StringBuilder csv = new();
            AppendRow(csv, "Artifact", "Schema", "DataJson");
            AppendRow(csv, artifact.Name, artifact.Schema, artifact.Data.GetRawText());
            return csv.ToString();
        }

        private static string JsonText(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => value.GetString() ?? string.Empty,
            _ => value.GetRawText(),
        };

        private static void AppendRow(StringBuilder builder, params string?[] values)
            => builder.AppendLine(string.Join(",", values.Select(Escape)));

        private static string Escape(string? value)
        {
            value ??= string.Empty;
            return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }

        private static string Adjacent(string directory, string stem, string artifactName)
            => Path.Combine(directory, $"{stem}_{Sanitize(artifactName)}.csv");

        private static string Sanitize(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = new(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "artifact" : sanitized;
        }

        private static string PrepareTarget(string filePath, bool overwrite)
        {
            string target = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(target);
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("The export path has no directory.", nameof(filePath));
            Directory.CreateDirectory(directory);
            if (!overwrite && File.Exists(target)) throw new IOException($"The export target already exists: {target}");
            return target;
        }

        private static void WriteAtomically(string target, string content, bool overwrite)
        {
            string directory = Path.GetDirectoryName(target)!;
            string temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                File.Move(temporary, target, overwrite);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
            }
        }
    }
}
