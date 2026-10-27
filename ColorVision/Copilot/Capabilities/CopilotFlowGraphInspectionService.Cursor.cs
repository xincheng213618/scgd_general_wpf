using System;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot;

public sealed partial class CopilotFlowGraphInspectionService
{
    private const string CursorPrefix = "cvfg1:";
    private sealed record Cursor(string SourceInstanceId, string Revision, string Section, string NodeId, bool IncludeProperties, string StreamKey, int Offset);

    private static string EncodeCursor(Cursor cursor) => CursorPrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1, source_instance = cursor.SourceInstanceId, revision = cursor.Revision, section = cursor.Section,
        node_id = cursor.NodeId, include_properties = cursor.IncludeProperties, stream_key = cursor.StreamKey, offset = cursor.Offset,
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
            if (properties.Length != 8 || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != 8
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1
                || !root.TryGetProperty("source_instance", out var source) || source.ValueKind != JsonValueKind.String || !IsHex(source.GetString(), 32)
                || !root.TryGetProperty("revision", out var revision) || revision.ValueKind != JsonValueKind.String || !IsHex(revision.GetString(), 64)
                || !root.TryGetProperty("section", out var section) || section.ValueKind != JsonValueKind.String || !IsSection(section.GetString())
                || !root.TryGetProperty("node_id", out var node) || node.ValueKind != JsonValueKind.String || node.GetString()!.Length > 256
                || !root.TryGetProperty("include_properties", out var include) || include.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !root.TryGetProperty("stream_key", out var key) || key.ValueKind != JsonValueKind.String || !IsHex(key.GetString(), 64)
                || !root.TryGetProperty("offset", out var offset) || !offset.TryGetInt32(out var start) || start < 0)
                return false;
            cursor = new(source.GetString()!.ToLowerInvariant(), revision.GetString()!.ToLowerInvariant(), section.GetString()!,
                node.GetString()!, include.GetBoolean(), key.GetString()!.ToLowerInvariant(), start);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException) { return false; }
    }
}
