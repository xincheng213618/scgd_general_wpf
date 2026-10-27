using System;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot;

public sealed partial class CopilotFlowRunEventInspectionService
{
    private const string CursorPrefix = "cvre1:";
    private sealed record Cursor(int RunRecordId, string SourceKey, long EventId, long SequenceNo);

    private static string EncodeCursor(Cursor cursor) => CursorPrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1, run_record_id = cursor.RunRecordId, source_key = cursor.SourceKey,
        event_id = cursor.EventId, sequence_no = cursor.SequenceNo,
    })).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecodeCursor(string text, out Cursor? cursor)
    {
        cursor = null;
        if (text.Length > 4096 || !text.StartsWith(CursorPrefix, StringComparison.Ordinal)) return false;
        var encoded = text[CursorPrefix.Length..];
        if (encoded.Length == 0 || encoded.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))) return false;
        try
        {
            var bytes = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + new string('=', (4 - encoded.Length % 4) % 4));
            if (Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') != encoded) return false;
            using var json = JsonDocument.Parse(bytes);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var properties = root.EnumerateObject().ToArray();
            if (properties.Length != 5 || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != 5
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1
                || !root.TryGetProperty("run_record_id", out var run) || !run.TryGetInt32(out var runId) || runId <= 0
                || !root.TryGetProperty("source_key", out var source) || source.ValueKind != JsonValueKind.String || !IsFingerprint(source.GetString())
                || !root.TryGetProperty("event_id", out var id) || !id.TryGetInt64(out var eventId) || eventId <= 0
                || !root.TryGetProperty("sequence_no", out var sequence) || !sequence.TryGetInt64(out var sequenceNo))
                return false;
            cursor = new(runId, source.GetString()!.ToLowerInvariant(), eventId, sequenceNo);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException) { return false; }
    }

    private static bool IsFingerprint(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
