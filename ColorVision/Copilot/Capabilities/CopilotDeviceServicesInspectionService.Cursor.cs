using System;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot;

public sealed partial class CopilotDeviceServicesInspectionService
{
    private const string CursorPrefix = "cvds1:";
    private sealed record Cursor(string SourceInstanceId, string InventoryRevision, string LastDeviceRef);
    private static string Encode(Cursor value) => CursorPrefix + Base64(JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1, source_instance_id = value.SourceInstanceId, inventory_revision = value.InventoryRevision, last_device_ref = value.LastDeviceRef,
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
            if (properties.Length != 4 || properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != 4
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1
                || !root.TryGetProperty("source_instance_id", out var source) || source.ValueKind != JsonValueKind.String || !IsHex(source.GetString(), 32)
                || !root.TryGetProperty("inventory_revision", out var revision) || revision.ValueKind != JsonValueKind.String || !IsHex(revision.GetString(), 64)
                || !root.TryGetProperty("last_device_ref", out var last) || last.ValueKind != JsonValueKind.String || !IsHex(last.GetString(), 32)) return false;
            cursor = new(source.GetString()!.ToLowerInvariant(), revision.GetString()!.ToLowerInvariant(), last.GetString()!.ToLowerInvariant());
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException) { return false; }
    }
    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
