using System;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot;

public sealed partial class CopilotLoadedBatchResultsInspectionService
{
    private const string CursorPrefix = "cvlbr1:";
    private sealed record Cursor(string RegistryInstanceId, string Mode, string? SourceRef, int? BatchId, string ScopeRevision, string LastRef);
    private static string Encode(Cursor value) => CursorPrefix + Base64(JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1, source_instance_id = value.RegistryInstanceId, mode = value.Mode, source_ref = value.SourceRef,
        batch_id = value.BatchId, scope_revision = value.ScopeRevision, last_ref = value.LastRef,
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
            if (properties.Length != 7 || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != 7
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1
                || !root.TryGetProperty("source_instance_id", out var registry) || registry.ValueKind != JsonValueKind.String || !IsHex(registry.GetString(), 32)
                || !root.TryGetProperty("mode", out var mode) || mode.ValueKind != JsonValueKind.String || mode.GetString() is not ("directory" or "results")
                || !root.TryGetProperty("source_ref", out var source) || !root.TryGetProperty("batch_id", out var batch)
                || !root.TryGetProperty("scope_revision", out var revision) || revision.ValueKind != JsonValueKind.String || !IsHex(revision.GetString(), 64)
                || !root.TryGetProperty("last_ref", out var last) || last.ValueKind != JsonValueKind.String || !IsHex(last.GetString(), 32)) return false;
            if (mode.GetString() == "directory" ? source.ValueKind != JsonValueKind.Null || batch.ValueKind != JsonValueKind.Null
                : source.ValueKind != JsonValueKind.String || !IsHex(source.GetString(), 32) || !batch.TryGetInt32(out _)) return false;
            cursor = new(registry.GetString()!.ToLowerInvariant(), mode.GetString()!, source.ValueKind == JsonValueKind.Null ? null : source.GetString()!.ToLowerInvariant(),
                batch.ValueKind == JsonValueKind.Null ? null : batch.GetInt32(), revision.GetString()!.ToLowerInvariant(), last.GetString()!.ToLowerInvariant());
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException) { return false; }
    }
    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
