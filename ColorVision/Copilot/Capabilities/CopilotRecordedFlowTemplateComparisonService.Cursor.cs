using ColorVision.Engine.Services.FlowDiagnostics;
using System;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot;

public sealed partial class CopilotRecordedFlowTemplateComparisonService
{
    private const string CursorPrefix = "cvtc1:";
    // Each side supports 10,000 nodes and 100,000 connections; the union/difference can contain both sides.
    private const int MaximumChangeRows = 220_000;

    private sealed record Cursor(int BaselineRunId, int CurrentRunId, string SourceKey, long BaselineSnapshotId,
        long CurrentSnapshotId, string BaselineHash, string CurrentHash, string IdentityBasis, int Offset)
    {
        public static Cursor From(FlowRecordedTemplatePairReadResult pair, int offset) => new(pair.Baseline!.Run.Id,
            pair.Current!.Run.Id, pair.SourceKey.ToLowerInvariant(), pair.Baseline.Snapshot!.Id, pair.Current.Snapshot!.Id,
            pair.Baseline.Snapshot.ContentHash.ToLowerInvariant(), pair.Current.Snapshot.ContentHash.ToLowerInvariant(), pair.IdentityBasis, offset);

        public bool Matches(FlowRecordedTemplatePairReadResult pair) => (this with { Offset = 0 }) == From(pair, 0);
    }

    private static string EncodeCursor(Cursor value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1, baseline_run_record_id = value.BaselineRunId, current_run_record_id = value.CurrentRunId,
            source_key = value.SourceKey, baseline_snapshot_id = value.BaselineSnapshotId, current_snapshot_id = value.CurrentSnapshotId,
            baseline_hash = value.BaselineHash, current_hash = value.CurrentHash, identity_basis = value.IdentityBasis, offset = value.Offset,
        });
        return CursorPrefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

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
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var properties = root.EnumerateObject().ToArray();
            if (properties.Length != 10 || properties.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != 10
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1
                || !root.TryGetProperty("baseline_run_record_id", out var baseline) || !baseline.TryGetInt32(out var baselineId) || baselineId <= 0
                || !root.TryGetProperty("current_run_record_id", out var current) || !current.TryGetInt32(out var currentId) || currentId <= 0
                || !root.TryGetProperty("source_key", out var source) || source.ValueKind != JsonValueKind.String || !IsFingerprint(source.GetString())
                || !root.TryGetProperty("baseline_snapshot_id", out var baselineSnapshot) || !baselineSnapshot.TryGetInt64(out var baselineSnapshotId) || baselineSnapshotId <= 0
                || !root.TryGetProperty("current_snapshot_id", out var currentSnapshot) || !currentSnapshot.TryGetInt64(out var currentSnapshotId) || currentSnapshotId <= 0
                || !root.TryGetProperty("baseline_hash", out var baselineHash) || baselineHash.ValueKind != JsonValueKind.String || !IsFingerprint(baselineHash.GetString())
                || !root.TryGetProperty("current_hash", out var currentHash) || currentHash.ValueKind != JsonValueKind.String || !IsFingerprint(currentHash.GetString())
                || !root.TryGetProperty("identity_basis", out var identity) || identity.ValueKind != JsonValueKind.String || identity.GetString() is not ("flow_key" or "legacy_template_id")
                || !root.TryGetProperty("offset", out var offset) || !offset.TryGetInt32(out var start) || start is < 0 or > MaximumChangeRows)
                return false;
            cursor = new(baselineId, currentId, source.GetString()!.ToLowerInvariant(), baselineSnapshotId, currentSnapshotId,
                baselineHash.GetString()!.ToLowerInvariant(), currentHash.GetString()!.ToLowerInvariant(), identity.GetString()!, start);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsFingerprint(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    private static CopilotCapabilityResult InvalidCursor() => Failure(CopilotToolFailureKind.Validation,
        "The comparison cursor is invalid, stale, or belongs to another source, run order, or retained template. Restart the comparison without a cursor.");
}
