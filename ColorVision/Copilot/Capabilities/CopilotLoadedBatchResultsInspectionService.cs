using ColorVision.Copilot.Mcp;
using ColorVision.Engine;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot;

public sealed partial class CopilotLoadedBatchResultsInspectionService
{
    internal const int MaximumContentCharacters = 18_000;
    private readonly Func<CancellationToken, Task<CopilotLoadedBatchResultsSnapshot>> _read;

    public CopilotLoadedBatchResultsInspectionService(Func<CancellationToken, Task<CopilotLoadedBatchResultsSnapshot>> read)
        => _read = read ?? throw new ArgumentNullException(nameof(read));

    public async Task<CopilotCapabilityResult> InspectAsync(string? sourceRef, string? expectedSourceInstanceId, string? resultRef, int maxItems, string? cursor, CancellationToken cancellationToken)
    {
        if (maxItems is < 1 or > 100 || sourceRef != null && !IsHex(sourceRef, 32)
            || expectedSourceInstanceId != null && !IsHex(expectedSourceInstanceId, 32) || resultRef != null && !IsHex(resultRef, 32)
            || sourceRef != null && expectedSourceInstanceId == null || resultRef != null && (sourceRef == null || cursor != null))
            return Failure(CopilotToolFailureKind.Validation, "Use max_items between 1 and 100 and exact 32-hex references. A source requires expected_source_instance_id; a result focus requires that source and cannot use a cursor.");
        sourceRef = sourceRef?.ToLowerInvariant();
        expectedSourceInstanceId = expectedSourceInstanceId?.ToLowerInvariant();
        resultRef = resultRef?.ToLowerInvariant();
        var mode = sourceRef == null ? "directory" : resultRef == null ? "results" : "focus";
        Cursor? continuation = null;
        if (cursor != null && (!TryDecode(cursor, out continuation) || continuation!.Mode != mode || continuation.SourceRef != sourceRef
            || expectedSourceInstanceId != null && continuation.RegistryInstanceId != expectedSourceInstanceId)) return InvalidScope();
        cancellationToken.ThrowIfCancellationRequested();
        CopilotLoadedBatchResultsSnapshot? snapshot;
        try
        {
            snapshot = await _read(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { throw; }
        catch (CopilotLoadedBatchResultsSourceUnavailableException)
        {
            return continuation != null || expectedSourceInstanceId != null ? InvalidScope()
                : Failure(CopilotToolFailureKind.NotFound, "The registered loaded batch sources are unavailable. No page, database query, or result was created.");
        }
        catch (CopilotLoadedBatchResultsSourceChangedException)
        {
            return Failure(CopilotToolFailureKind.Validation, "Registered loaded sources changed during capture; no evidence was published. Retry the capture. Rediscover references if the bound registry or member inventory changed.");
        }
        catch (CopilotLoadedBatchResultsSourceInvalidException) { return InvalidSource(); }
        catch (TimeoutException)
        {
            return Failure(CopilotToolFailureKind.Transient, "The loaded batch-result sources did not respond in time. No database query or result handler was invoked.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(CopilotToolFailureKind.Authorization, "The loaded batch-result registry could not be read.");
        }
        catch (IOException)
        {
            return Failure(CopilotToolFailureKind.Transient, "The loaded batch-result registry is temporarily unavailable.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException) { return InvalidSource(); }
        if (snapshot == null) return continuation != null || expectedSourceInstanceId != null ? InvalidScope()
            : Failure(CopilotToolFailureKind.NotFound, "No loaded batch-result registry is available. No page, database query, or result was created.");
        if (!IsHex(snapshot.RegistryInstanceId, 32) || snapshot.Sources == null) return InvalidSource();
        var registry = snapshot.RegistryInstanceId.ToLowerInvariant();
        if (expectedSourceInstanceId != null && expectedSourceInstanceId != registry || continuation != null && continuation.RegistryInstanceId != registry) return InvalidScope();
        var sourceRefs = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in snapshot.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (source == null || !IsHex(source.SourceRef, 32) || !sourceRefs.Add(source.SourceRef) || source.Results == null) return InvalidSource();
            var resultRefs = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in source.Results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row == null || !IsHex(row.ResultRef, 32) || !resultRefs.Add(row.ResultRef) || row.Kind is not ("image" or "algorithm") || row.TemplateName == null) return InvalidSource();
            }
        }
        var sources = snapshot.Sources.OrderBy(source => source.SourceRef.ToLowerInvariant(), StringComparer.Ordinal).ToArray();
        var sourcesRevision = Membership(sources.Select(source => source.SourceRef));
        var selected = sourceRef == null ? null : sources.FirstOrDefault(source => string.Equals(source.SourceRef, sourceRef, StringComparison.OrdinalIgnoreCase));
        if (sourceRef != null && selected == null) return continuation != null ? InvalidScope()
            : Failure(CopilotToolFailureKind.NotFound, "The exact loaded source_ref is no longer registered. No current-page, batch-id, or name fallback was used.");
        var results = selected?.Results.OrderBy(row => row.ResultRef.ToLowerInvariant(), StringComparer.Ordinal).ToArray() ?? [];
        var inventoryRevision = selected == null ? null : Membership(results.Select(row => row.ResultRef));
        var scopeRevision = selected == null ? sourcesRevision : inventoryRevision!;
        if (continuation != null && (continuation.ScopeRevision != scopeRevision || continuation.BatchId != selected?.BatchId
            || (selected == null ? !sourceRefs.Contains(continuation.LastRef) : !results.Any(row => string.Equals(row.ResultRef, continuation.LastRef, StringComparison.OrdinalIgnoreCase))))) return InvalidScope();
        var matchedResults = resultRef == null ? results : results.Where(row => string.Equals(row.ResultRef, resultRef, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (resultRef != null && matchedResults.Length == 0)
            return Failure(CopilotToolFailureKind.NotFound, "The exact result_ref is absent from this loaded source. No recorded id, kind, template, or selected-result fallback was used.");
        var remainingSources = sourceRef == null ? sources.Where(source => continuation == null || string.CompareOrdinal(source.SourceRef.ToLowerInvariant(), continuation.LastRef) > 0).ToArray() : [];
        var remainingResults = matchedResults.Where(row => continuation == null || string.CompareOrdinal(row.ResultRef.ToLowerInvariant(), continuation.LastRef) > 0).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var result = Format(snapshot, mode, selected, sourcesRevision, inventoryRevision, scopeRevision,
            remainingSources, remainingResults, matchedResults.Length, maxItems, continuation != null);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static CopilotCapabilityResult Format(CopilotLoadedBatchResultsSnapshot snapshot, string mode, CopilotLoadedBatchSourceSnapshot? source,
        string sourcesRevision, string? inventoryRevision, string scopeRevision, CopilotLoadedBatchSourceSnapshot[] remainingSources,
        CopilotLoadedBatchResultRow[] remainingResults, int matchedResults, int maximum, bool isContinuation)
    {
        var textTruncated = false;
        var textRedacted = false;
        string Text(string value)
        {
            var text = CopilotMcpAuditLogger.RedactText(value);
            textRedacted |= text != value;
            if (text.Length <= 256) return text;
            textTruncated = true;
            var length = char.IsHighSurrogate(text[255]) && char.IsLowSurrogate(text[256]) ? 255 : 256;
            return text[..length];
        }
        var directory = remainingSources.Take(maximum).Select(item => new
        {
            source_ref = item.SourceRef.ToLowerInvariant(), batch_id = item.BatchId, total_results = item.Results.Count,
            image_results = item.Results.Count(row => row.Kind == "image"), algorithm_results = item.Results.Count(row => row.Kind == "algorithm"),
        }).ToList();
        var rows = remainingResults.Take(mode == "focus" ? 1 : maximum).Select(row => new
        {
            result_ref = row.ResultRef.ToLowerInvariant(), kind = row.Kind, internal_id = row.InternalId,
            type_code = row.TypeCode, template_name = Text(row.TemplateName), result_code = row.ResultCode,
            recorded_duration_ms = row.DurationMs, duration_ms = row.DurationMs is >= 0 ? row.DurationMs : null,
            created_at = row.CreatedAt, created_at_kind = row.CreatedAt?.Kind.ToString(),
        }).ToList();
        var outputLimited = false;
        int Returned() => mode == "directory" ? directory.Count : rows.Count;
        bool HasMore() => mode != "focus" && Returned() < (mode == "directory" ? remainingSources.Length : remainingResults.Length);
        string? NextCursor() => !HasMore() || Returned() == 0 ? null : Encode(new Cursor(snapshot.RegistryInstanceId.ToLowerInvariant(), mode,
            source?.SourceRef.ToLowerInvariant(), source?.BatchId, scopeRevision, mode == "directory" ? directory[^1].source_ref : rows[^1].result_ref));
        string Serialize() => JsonSerializer.Serialize(new
        {
            source = "registered_loaded_batch_details", mode, source_instance_id = snapshot.RegistryInstanceId.ToLowerInvariant(),
            sources_revision = sourcesRevision, inventory_revision = inventoryRevision, observed_at_utc = snapshot.ObservedAtUtc.ToUniversalTime(),
            source_ref = source?.SourceRef.ToLowerInvariant(), batch_id = source?.BatchId,
            total_sources = snapshot.Sources.Count, matched_sources = source == null ? snapshot.Sources.Count : 1,
            total_results = source == null ? snapshot.Sources.Sum(item => (long)item.Results.Count) : source.Results.Count,
            matched_results = source == null ? (int?)null : matchedResults,
            sources = directory, results = rows, has_more = HasMore(), next_cursor = NextCursor(),
            interpretation = "Only registered, already-loaded batch detail collections were read; this is not database history or the current UI selection. References identify one source registration and its row objects; kind and internal_id are recorded metadata, not selectors or flow run ids. Result codes are raw nullable values, not customer PASS/FAIL. Duration is a loaded raw millisecond value, never parsed from formatted image time; negative/unknown values have no valid duration. Created dates retain their recorded DateTime kind and are not relabeled UTC. No SN, path, message, payload, file-existence probe, result handler, export, UI navigation, or database query is performed.",
            coverage = new
            {
                returned_sources = directory.Count, returned_results = rows.Count, text_redacted = textRedacted, text_truncated = textTruncated, output_limited = outputLimited,
                all_matched_sources_returned = source == null && directory.Count == snapshot.Sources.Count,
                all_matched_results_returned = source != null && rows.Count == matchedResults,
                unknown_result_code_rows = rows.Count(row => row.result_code == null),
                unknown_duration_rows = rows.Count(row => row.recorded_duration_ms == null), invalid_duration_rows = rows.Count(row => row.recorded_duration_ms is < 0),
                unspecified_created_at_rows = rows.Count(row => row.created_at?.Kind == DateTimeKind.Unspecified),
                query_scope = "Directory cursors bind registry/source-reference membership. Result cursors bind their exact source, recorded batch id and that source's result-object membership; unrelated registrations may change. Focus is a fresh exact-reference lookup. A source change during capture can require retrying the capture before checking cursor validity. Metadata may change live; finishing pages does not establish complete database or execution history.",
                ordering = "source_ref or result_ref ordinal ascending; cursors advance only after the last row actually delivered",
            },
        });
        var content = Serialize();
        while (content.Length > MaximumContentCharacters && Returned() > 1)
        {
            if (mode == "directory") directory.RemoveAt(directory.Count - 1);
            else rows.RemoveAt(rows.Count - 1);
            outputLimited = true;
            content = Serialize();
        }
        if (content.Length > MaximumContentCharacters) return Failure(CopilotToolFailureKind.Validation, "The loaded-result evidence could not fit the bounded output contract; continuation was not advanced.");
        return new CopilotCapabilityResult
        {
            Success = true, Summary = $"Read {Returned()} registered loaded batch {(mode == "directory" ? "sources" : "result records")}.", Content = content,
            PartialResultMessage = isContinuation || HasMore() || textRedacted || textTruncated || rows.Any(row => row.result_code == null || row.duration_ms == null || row.created_at == null)
                ? "Only the indicated registered loaded-data window was read. Inspect coverage and raw unknown values; use next_cursor unchanged when present."
                : string.Empty,
        };
    }

    private static string Membership(System.Collections.Generic.IEnumerable<string> references)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", references.Select(value => value.ToLowerInvariant()).Order(StringComparer.Ordinal))))).ToLowerInvariant();
    private static bool IsHex(string? value, int length) => value != null && value.Length == length && value.All(char.IsAsciiHexDigit);
    private static CopilotCapabilityResult InvalidSource() => Failure(CopilotToolFailureKind.Internal, "The loaded batch source returned inconsistent source, row, or identity evidence; no result data was published.");
    private static CopilotCapabilityResult InvalidScope() => Failure(CopilotToolFailureKind.Validation, "The loaded-result cursor or expected owner is invalid, stale, or belongs to another source or member inventory. Restart the directory to obtain exact references.");
    private static CopilotCapabilityResult Failure(CopilotToolFailureKind kind, string message) => new()
    {
        Success = false, FailureKind = kind, Summary = "Loaded batch results were not inspected.", ErrorMessage = message,
    };
}
