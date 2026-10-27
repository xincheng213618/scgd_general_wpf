using System;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot;

public sealed partial class CopilotFlowRunAttemptInspectionService
{
    private const string CursorPrefix = "cvra1:";
    private sealed record Cursor(int RunRecordId, string SourceKey, string NodeKey, long AttemptId);

    private static string Encode(Cursor cursor) => CursorPrefix + Base64(JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1, run_record_id = cursor.RunRecordId, source_key = cursor.SourceKey, node_key = cursor.NodeKey, attempt_id = cursor.AttemptId,
    }));

    private static bool TryDecode(string value, out Cursor? cursor)
    {
        cursor = null;
        if (value.Length > 4096 || !value.StartsWith(CursorPrefix, StringComparison.Ordinal)) return false;
        var encoded = value[CursorPrefix.Length..];
        if (encoded.Length == 0 || encoded.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))) return false;
        try
        {
            var bytes = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + new string('=', (4 - encoded.Length % 4) % 4));
            if (Base64(bytes) != encoded) return false;
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var properties = root.EnumerateObject().ToArray();
            if (properties.Length != 5 || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != 5
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1
                || !root.TryGetProperty("run_record_id", out var run) || !run.TryGetInt32(out var runId) || runId <= 0
                || !root.TryGetProperty("source_key", out var source) || source.ValueKind != JsonValueKind.String || !IsFingerprint(source.GetString())
                || !root.TryGetProperty("node_key", out var node) || node.ValueKind != JsonValueKind.String || !IsFingerprint(node.GetString())
                || !root.TryGetProperty("attempt_id", out var id) || !id.TryGetInt64(out var attemptId) || attemptId <= 0)
                return false;
            cursor = new(runId, source.GetString()!.ToLowerInvariant(), node.GetString()!.ToLowerInvariant(), attemptId);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException) { return false; }
    }

    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static bool IsFingerprint(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
