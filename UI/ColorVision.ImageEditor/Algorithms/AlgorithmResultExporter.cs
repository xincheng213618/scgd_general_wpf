using ColorVision.Algorithms;
using ColorVision.ImageEditor.Output;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Streams host-neutral analysis artifacts to atomically replaced files.</summary>
    public static class AlgorithmResultExporter
    {
        private const int StructuredJsonFlushBytes = 128 * 1024;
        private static readonly SearchValues<char> CsvQuoteCharacters = SearchValues.Create(",\"\r\n");
        private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

        public static string ExportJson(AlgorithmResult result, string filePath, bool overwrite = false)
            => ExportJsonAsync(result, filePath, overwrite, CancellationToken.None).GetAwaiter().GetResult();

        public static async Task<string> ExportJsonAsync(
            AlgorithmResult result,
            string filePath,
            bool overwrite = false,
            CancellationToken cancellationToken = default,
            IProgress<AlgorithmProgress>? progress = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            string target = PrepareTarget(filePath, overwrite);
            string temporary = TemporaryFor(target);
            try
            {
                await using (FileStream stream = new(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await stream.WriteAsync(Utf8WithBom.GetPreamble(), cancellationToken).ConfigureAwait(false);
                    using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });
                    writer.WriteStartObject();
                    writer.WriteString("invocationId", result.InvocationId);
                    writer.WriteString("algorithmId", result.AlgorithmId.Value);
                    writer.WriteString("algorithmVersion", result.AlgorithmVersion.ToString());
                    writer.WriteString("status", result.Status.ToString());
                    writer.WritePropertyName("artifacts");
                    writer.WriteStartArray();
                    int completed = 0;
                    foreach (AlgorithmArtifact artifact in result.Artifacts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await WriteArtifactJsonAsync(writer, artifact, cancellationToken).ConfigureAwait(false);
                        completed++;
                        progress?.Report(new AlgorithmProgress(
                            result.Artifacts.Count == 0 ? 1 : completed / (double)result.Artifacts.Count,
                            "export-json",
                            artifact.Name));
                        if ((completed & 127) == 0) await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    writer.WriteEndArray();
                    writer.WritePropertyName("failures");
                    JsonSerializer.Serialize(writer, result.Failures, AlgorithmJson.Options);
                    writer.WritePropertyName("diagnostics");
                    JsonSerializer.Serialize(writer, result.Diagnostics, AlgorithmJson.Options);
                    writer.WriteEndObject();
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, target, overwrite);
                progress?.Report(new AlgorithmProgress(1, "export-json", "complete"));
                return target;
            }
            finally
            {
                TryDelete(temporary);
            }
        }

        private static async Task WriteArtifactJsonAsync(
            Utf8JsonWriter writer,
            AlgorithmArtifact artifact,
            CancellationToken cancellationToken)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", artifact switch
            {
                AlgorithmImageArtifact => "image",
                AlgorithmMeasurementArtifact => "measurement",
                AlgorithmTableArtifact => "table",
                AlgorithmGeometryArtifact => "geometry",
                AlgorithmStructuredDataArtifact => "structuredData",
                AlgorithmOverlayArtifact => "overlay",
                _ => throw new NotSupportedException($"Unsupported algorithm artifact type: {artifact.GetType().FullName}"),
            });
            writer.WriteString("name", artifact.Name);
            switch (artifact)
            {
                case AlgorithmImageArtifact image:
                    writer.WriteString("role", image.Role);
                    writer.WritePropertyName("metadata");
                    JsonSerializer.Serialize(writer, image.Metadata, AlgorithmJson.Options);
                    break;
                case AlgorithmMeasurementArtifact measurements:
                    writer.WritePropertyName("measurements");
                    writer.WriteStartArray();
                    for (int index = 0; index < measurements.Measurements.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        JsonSerializer.Serialize(writer, measurements.Measurements[index], AlgorithmJson.Options);
                        if ((index & 1023) == 1023) await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    writer.WriteEndArray();
                    break;
                case AlgorithmTableArtifact table:
                    writer.WritePropertyName("columns");
                    JsonSerializer.Serialize(writer, table.Columns, AlgorithmJson.Options);
                    writer.WritePropertyName("rows");
                    writer.WriteStartArray();
                    for (int index = 0; index < table.Rows.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        JsonSerializer.Serialize(writer, table.Rows[index], AlgorithmJson.Options);
                        if ((index & 1023) == 1023) await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    writer.WriteEndArray();
                    break;
                case AlgorithmGeometryArtifact geometry:
                    writer.WritePropertyName("coordinateSpace");
                    JsonSerializer.Serialize(writer, geometry.CoordinateSpace, AlgorithmJson.Options);
                    writer.WritePropertyName("geometries");
                    writer.WriteStartArray();
                    for (int index = 0; index < geometry.Geometries.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        JsonSerializer.Serialize(writer, geometry.Geometries[index], AlgorithmJson.Options);
                        if ((index & 1023) == 1023) await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    writer.WriteEndArray();
                    break;
                case AlgorithmStructuredDataArtifact structured:
                    writer.WriteString("schema", structured.Schema);
                    writer.WritePropertyName("data");
                    await WriteJsonElementAsync(writer, structured.Data, cancellationToken).ConfigureAwait(false);
                    break;
                case AlgorithmOverlayArtifact overlay:
                    writer.WritePropertyName("lifetime");
                    JsonSerializer.Serialize(writer, overlay.Lifetime, AlgorithmJson.Options);
                    writer.WritePropertyName("items");
                    JsonSerializer.Serialize(writer, overlay.Items, AlgorithmJson.Options);
                    break;
            }
            writer.WriteEndObject();
        }

        // Utf8JsonWriter retains pending data until flushed. Keep the traversal and synchronous
        // buffer flushes on a worker so a large structured artifact cannot block the dispatcher.
        private static Task WriteJsonElementAsync(Utf8JsonWriter writer, JsonElement value, CancellationToken cancellationToken)
            => Task.Run(() => WriteJsonElement(writer, value, cancellationToken), cancellationToken);

        private static void WriteJsonElement(Utf8JsonWriter writer, JsonElement value, CancellationToken cancellationToken)
        {
            FlushStructuredJsonIfNeeded(writer, cancellationToken);
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (JsonProperty property in value.EnumerateObject())
                    {
                        if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        {
                            writer.WritePropertyName(property.Name);
                            WriteJsonElement(writer, property.Value, cancellationToken);
                        }
                        else
                        {
                            FlushStructuredJsonIfNeeded(writer, cancellationToken);
                            property.WriteTo(writer);
                        }
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (JsonElement item in value.EnumerateArray())
                        WriteJsonElement(writer, item, cancellationToken);
                    writer.WriteEndArray();
                    break;
                default:
                    value.WriteTo(writer);
                    break;
            }
        }

        private static void FlushStructuredJsonIfNeeded(Utf8JsonWriter writer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (writer.BytesPending >= StructuredJsonFlushBytes)
                writer.Flush();
        }

        public static IReadOnlyList<string> ExportCsvBundle(AlgorithmResult result, string filePath, bool overwrite = false)
            => ExportCsvBundleAsync(result, filePath, overwrite, CancellationToken.None).GetAwaiter().GetResult();

        internal static IEnumerable<string> GetCsvBundlePaths(AlgorithmResult result, string filePath)
            => CreateCsvOutputs(result, filePath).Select(output => output.Path);

        /// <summary>
        /// Writes measurements to the selected CSV path and each Table/Geometry/StructuredData
        /// artifact to an adjacent suffixed CSV. All content is staged before any target is
        /// replaced, so cancellation never damages an existing export.
        /// </summary>
        public static async Task<IReadOnlyList<string>> ExportCsvBundleAsync(
            AlgorithmResult result,
            string filePath,
            bool overwrite = false,
            CancellationToken cancellationToken = default,
            IProgress<AlgorithmProgress>? progress = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            CsvOutput[] outputs = CreateCsvOutputs(result, filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputs[0].Path)!);
            ValidateTargets(outputs.Select(output => output.Path).ToArray(), overwrite);
            long totalRows = Math.Max(1, outputs.Sum(output => output.EstimatedRows));
            long completedRows = 0;
            List<StagedFilePublication.Entry> staged = [];
            try
            {
                foreach (CsvOutput output in outputs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string temporary = TemporaryFor(output.Path);
                    staged.Add(new StagedFilePublication.Entry(temporary, output.Path));
                    await WriteCsvOutputAsync(
                        result,
                        output,
                        temporary,
                        cancellationToken,
                        rows =>
                        {
                            completedRows += rows;
                            progress?.Report(new AlgorithmProgress(
                                Math.Min(1, completedRows / (double)totalRows),
                                "export-csv",
                                output.DisplayName));
                        }).ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
                StagedFilePublication.Commit(staged, overwrite, cancellationToken);
                progress?.Report(new AlgorithmProgress(1, "export-csv", "complete"));
                return outputs.Select(output => output.Path).ToArray();
            }
            finally
            {
                foreach (StagedFilePublication.Entry file in staged) TryDelete(file.Temporary);
            }
        }

        private static CsvOutput[] CreateCsvOutputs(AlgorithmResult result, string filePath)
        {
            string primary = Path.GetFullPath(filePath);
            string directory = Path.GetDirectoryName(primary) ?? throw new ArgumentException("The export path has no directory.", nameof(filePath));
            string stem = Path.GetFileNameWithoutExtension(primary);
            if (string.IsNullOrWhiteSpace(stem)) throw new ArgumentException("The export path has no file name.", nameof(filePath));

            List<CsvOutput> outputs =
            [
                new(primary, CsvOutputKind.Measurements, null, "measurements",
                    Math.Max(1, result.Artifacts.OfType<AlgorithmMeasurementArtifact>().Sum(value => (long)value.Measurements.Count))),
            ];
            outputs.AddRange(result.Artifacts.OfType<AlgorithmTableArtifact>()
                .Select(table => new CsvOutput(Adjacent(directory, stem, table.Name), CsvOutputKind.Table, table, table.Name, Math.Max(1, table.Rows.Count))));
            outputs.AddRange(result.Artifacts.OfType<AlgorithmGeometryArtifact>()
                .Select(geometry => new CsvOutput(Adjacent(directory, stem, geometry.Name), CsvOutputKind.Geometry, geometry, geometry.Name, Math.Max(1, geometry.Geometries.Count))));
            outputs.AddRange(result.Artifacts.OfType<AlgorithmStructuredDataArtifact>()
                .Select(structured => new CsvOutput(Adjacent(directory, stem, structured.Name), CsvOutputKind.Structured, structured, structured.Name, 1)));
            return outputs.ToArray();
        }

        private static void ValidateTargets(IReadOnlyList<string> targets, bool overwrite)
        {
            string? duplicate = targets.GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicate != null) throw new InvalidOperationException($"Artifact export paths collide: {duplicate}");
            if (!overwrite)
            {
                string? existing = targets.FirstOrDefault(File.Exists);
                if (existing != null) throw new IOException($"The export target already exists: {existing}");
            }
        }

        private static async Task WriteCsvOutputAsync(
            AlgorithmResult result,
            CsvOutput output,
            string temporary,
            CancellationToken cancellationToken,
            Action<int> reportRows)
        {
            await using FileStream stream = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using StreamWriter writer = new(stream, Utf8WithBom, bufferSize: 64 * 1024, leaveOpen: true);
            CsvFieldBuffer csv = new(writer);
            switch (output.Kind)
            {
                case CsvOutputKind.Measurements:
                    await WriteMeasurementsAsync(result, csv, cancellationToken, reportRows).ConfigureAwait(false);
                    break;
                case CsvOutputKind.Table:
                    await WriteTableAsync((AlgorithmTableArtifact)output.Artifact!, csv, cancellationToken, reportRows).ConfigureAwait(false);
                    break;
                case CsvOutputKind.Geometry:
                    await WriteGeometryAsync((AlgorithmGeometryArtifact)output.Artifact!, csv, cancellationToken, reportRows).ConfigureAwait(false);
                    break;
                case CsvOutputKind.Structured:
                    await WriteStructuredAsync((AlgorithmStructuredDataArtifact)output.Artifact!, csv, writer, stream, cancellationToken, reportRows).ConfigureAwait(false);
                    break;
            }
            await csv.FlushAsync(cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task WriteMeasurementsAsync(
            AlgorithmResult result,
            CsvFieldBuffer writer,
            CancellationToken cancellationToken,
            Action<int> reportRows)
        {
            await WriteRowAsync(writer, ["Artifact", "Name", "Value", "Unit", "Channel", "Confidence", "QualifiersJson"], cancellationToken).ConfigureAwait(false);
            foreach (AlgorithmMeasurementArtifact artifact in result.Artifacts.OfType<AlgorithmMeasurementArtifact>())
            {
                foreach (AlgorithmMeasurement measurement in artifact.Measurements)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await WriteRowAsync(writer,
                    [
                        artifact.Name,
                        measurement.Name,
                        measurement.Value.ToString("R", CultureInfo.InvariantCulture),
                        measurement.Unit,
                        measurement.Channel?.ToString(CultureInfo.InvariantCulture),
                        measurement.Confidence?.ToString("R", CultureInfo.InvariantCulture),
                        measurement.Qualifiers == null ? null : JsonSerializer.Serialize(measurement.Qualifiers, AlgorithmJson.Options),
                    ], cancellationToken).ConfigureAwait(false);
                    reportRows(1);
                }
            }
        }

        private static async Task WriteTableAsync(
            AlgorithmTableArtifact table,
            CsvFieldBuffer writer,
            CancellationToken cancellationToken,
            Action<int> reportRows)
        {
            await WriteRowAsync(writer, table.Columns.Select(column => column.Name), cancellationToken).ConfigureAwait(false);
            foreach (IReadOnlyDictionary<string, JsonElement> row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool first = true;
                foreach (AlgorithmTableColumn column in table.Columns)
                {
                    await writer.WriteFieldAsync(row.TryGetValue(column.Name, out JsonElement value) ? JsonText(value) : null, first, cancellationToken).ConfigureAwait(false);
                    first = false;
                }
                await writer.AppendAsync(Environment.NewLine.AsMemory(), cancellationToken).ConfigureAwait(false);
                reportRows(1);
            }
        }

        private static async Task WriteGeometryAsync(
            AlgorithmGeometryArtifact artifact,
            CsvFieldBuffer writer,
            CancellationToken cancellationToken,
            Action<int> reportRows)
        {
            await WriteRowAsync(writer,
                ["Artifact", "CoordinateSpace", "Id", "Kind", "PointsJson", "Radius", "MatrixJson", "Residual", "Confidence", "FilterReason", "MeasurementsJson"],
                cancellationToken).ConfigureAwait(false);
            foreach (AlgorithmGeometry geometry in artifact.Geometries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteRowAsync(writer,
                [
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
                    geometry.Measurements == null ? null : JsonSerializer.Serialize(geometry.Measurements, AlgorithmJson.Options),
                ], cancellationToken).ConfigureAwait(false);
                reportRows(1);
            }
        }

        private static async Task WriteStructuredAsync(
            AlgorithmStructuredDataArtifact artifact,
            CsvFieldBuffer csv,
            StreamWriter writer,
            FileStream stream,
            CancellationToken cancellationToken,
            Action<int> reportRows)
        {
            await WriteRowAsync(csv, ["Artifact", "Schema", "DataJson"], cancellationToken).ConfigureAwait(false);
            await csv.WriteFieldAsync(artifact.Name, first: true, cancellationToken).ConfigureAwait(false);
            await csv.WriteFieldAsync(artifact.Schema, first: false, cancellationToken).ConfigureAwait(false);
            await csv.AppendAsync(",\"".AsMemory(), cancellationToken).ConfigureAwait(false);
            await csv.FlushAsync(cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            using (CsvJsonEscapingBuffer escaping = new(stream, cancellationToken))
            using (Utf8JsonWriter json = new(escaping))
            {
                await Task.Run(() =>
                {
                    artifact.Data.WriteTo(json);
                    json.Flush();
                    escaping.Flush();
                }, cancellationToken).ConfigureAwait(false);
            }
            await stream.WriteAsync("\"\r\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            reportRows(1);
        }

        private static async Task WriteRowAsync(CsvFieldBuffer writer, IEnumerable<string?> values, CancellationToken cancellationToken)
        {
            bool first = true;
            foreach (string? value in values)
            {
                await writer.WriteFieldAsync(value, first, cancellationToken).ConfigureAwait(false);
                first = false;
            }
            await writer.AppendAsync(Environment.NewLine.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        private static string JsonText(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => value.GetString() ?? string.Empty,
            _ => value.GetRawText(),
        };

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

        private static string TemporaryFor(string target)
            => Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private enum CsvOutputKind { Measurements, Table, Geometry, Structured }

        private sealed record CsvOutput(string Path, CsvOutputKind Kind, AlgorithmArtifact? Artifact, string DisplayName, long EstimatedRows);


        private sealed class CsvFieldBuffer(StreamWriter writer)
        {
            // Bound memory independently of field or row length.
            private readonly char[] _buffer = new char[16 * 1024];
            private int _count;

            public async ValueTask WriteFieldAsync(string? value, bool first, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!first) await AppendAsync(",".AsMemory(), cancellationToken).ConfigureAwait(false);
                value ??= string.Empty;
                if (value.AsSpan().IndexOfAny(CsvQuoteCharacters) < 0)
                {
                    await AppendAsync(value.AsMemory(), cancellationToken).ConfigureAwait(false);
                    return;
                }
                await AppendAsync("\"".AsMemory(), cancellationToken).ConfigureAwait(false);
                int start = 0;
                while (true)
                {
                    int index = value.IndexOf('"', start);
                    if (index < 0) break;
                    await AppendAsync(value.AsMemory(start, index - start), cancellationToken).ConfigureAwait(false);
                    await AppendAsync("\"\"".AsMemory(), cancellationToken).ConfigureAwait(false);
                    start = index + 1;
                }
                await AppendAsync(value.AsMemory(start), cancellationToken).ConfigureAwait(false);
                await AppendAsync("\"".AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            public ValueTask AppendAsync(ReadOnlyMemory<char> value, CancellationToken cancellationToken)
            {
                if (value.Length <= _buffer.Length - _count)
                {
                    value.Span.CopyTo(_buffer.AsSpan(_count));
                    _count += value.Length;
                    return ValueTask.CompletedTask;
                }
                return AppendSlowAsync(value, cancellationToken);
            }

            private async ValueTask AppendSlowAsync(ReadOnlyMemory<char> value, CancellationToken cancellationToken)
            {
                while (!value.IsEmpty)
                {
                    if (_count == _buffer.Length) await FlushAsync(cancellationToken).ConfigureAwait(false);
                    int length = Math.Min(value.Length, _buffer.Length - _count);
                    value.Span[..length].CopyTo(_buffer.AsSpan(_count));
                    _count += length;
                    value = value[length..];
                }
            }

            public async ValueTask FlushAsync(CancellationToken cancellationToken)
            {
                if (_count == 0) return;
                await writer.WriteAsync(_buffer.AsMemory(0, _count), cancellationToken).ConfigureAwait(false);
                _count = 0;
            }
        }

        // Flush between writer buffer requests and grow only for a single large token.
        private sealed class CsvJsonEscapingBuffer(Stream stream, CancellationToken cancellationToken) : IBufferWriter<byte>, IDisposable
        {
            private byte[] _buffer = ArrayPool<byte>.Shared.Rent(StructuredJsonFlushBytes);
            private int _pending;

            public void Advance(int count) => _pending += count;

            public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sizeHint = Math.Max(sizeHint, 1);
                if (_buffer.Length - _pending < sizeHint)
                {
                    Flush();
                    if (_buffer.Length < sizeHint)
                    {
                        byte[] replacement = ArrayPool<byte>.Shared.Rent(sizeHint);
                        ArrayPool<byte>.Shared.Return(_buffer);
                        _buffer = replacement;
                    }
                }
                return _buffer.AsMemory(_pending);
            }

            public void Flush()
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadOnlySpan<byte> pending = _buffer.AsSpan(0, _pending);
                Span<byte> escaped = stackalloc byte[16 * 1024];
                while (!pending.IsEmpty)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = Math.Min(pending.Length, escaped.Length / 2);
                    int written = 0;
                    foreach (byte value in pending[..count])
                    {
                        escaped[written] = value;
                        escaped[written + 1] = value;
                        // Only a quote keeps its second copy.
                        written += value == (byte)'"' ? 2 : 1;
                    }
                    stream.Write(escaped[..written]);
                    pending = pending[count..];
                }
                _pending = 0;
            }

            public void Dispose() => ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}
