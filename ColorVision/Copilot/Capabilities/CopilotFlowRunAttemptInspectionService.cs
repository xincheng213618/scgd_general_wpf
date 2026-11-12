using ColorVision.Copilot.Mcp;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using ColorVision.UI;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot;

public sealed partial class CopilotFlowRunAttemptInspectionService
{
    internal const int MaximumContentCharacters = 18_000;
    private readonly Func<int, string?, int, long?, CancellationToken, Task<FlowRunAttemptPage?>> _read;

    public CopilotFlowRunAttemptInspectionService() : this((run, node, maximum, before, token) => new FlowRunAttemptReader(
        ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath).ReadPageAsync(run, node, maximum, before, token)) { }

    public CopilotFlowRunAttemptInspectionService(Func<int, string?, int, long?, CancellationToken, Task<FlowRunAttemptPage?>> read)
        => _read = read ?? throw new ArgumentNullException(nameof(read));

    public async Task<CopilotCapabilityResult> InspectAsync(int runRecordId, string? nodeId, int maxAttempts, string? cursor, CancellationToken cancellationToken)
    {
        if (runRecordId <= 0 || maxAttempts is < 1 or > 100 || nodeId != null && (nodeId.Length > 128 || string.IsNullOrWhiteSpace(nodeId)))
            return Failure(CopilotToolFailureKind.Validation, "Use a positive run_record_id, max_attempts between 1 and 100, and an optional exact nonblank node_id of at most 128 characters.");
        var nodeKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nodeId == null ? "all" : "node:" + nodeId))).ToLowerInvariant();
        Cursor? continuation = null;
        if (cursor != null && (!TryDecode(cursor, out continuation) || continuation!.RunRecordId != runRecordId || continuation.NodeKey != nodeKey))
            return InvalidCursor();
        cancellationToken.ThrowIfCancellationRequested();
        FlowRunAttemptPage? page;
        try
        {
            page = await _read(runRecordId, nodeId, maxAttempts, continuation?.AttemptId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Failure(CopilotToolFailureKind.NotFound, "The local flow journal is unavailable. This does not establish that the run or its attempts are absent; no database was created.");
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or InvalidCastException or OverflowException)
        {
            return Failure(CopilotToolFailureKind.Validation, "The local journal lacks valid required run or attempt evidence. No migration was performed and no attempt window was published.");
        }
        catch (ArgumentException) { return InvalidCursor(); }
        catch (UnauthorizedAccessException)
        {
            return Failure(CopilotToolFailureKind.Authorization, "The local flow journal could not be opened for read-only attempt inspection.");
        }
        catch (Exception exception) when (exception is IOException or SqliteException)
        {
            return Failure(CopilotToolFailureKind.Transient, "The local attempt evidence could not be read. Retry after the journal becomes available; no flow was changed or rerun.");
        }
        if (page == null)
            return continuation != null ? InvalidCursor() : Failure(CopilotToolFailureKind.NotFound, "The exact run_record_id is not retained in the local journal.");
        if (continuation != null && !string.Equals(page.SourceKey, continuation.SourceKey, StringComparison.OrdinalIgnoreCase))
            return InvalidCursor();
        if (page.RunRecordId != runRecordId || !IsFingerprint(page.SourceKey) || page.Attempts == null || page.Attempts.Count > maxAttempts
            || page.OutcomeTruncatedAttemptIds == null
            || page.OutcomeTruncatedAttemptIds.Any(id => id <= 0 || !page.Attempts.Any(item => item != null && item.Id == id))
            || page.OutcomeTruncatedAttemptIds.Distinct().Count() != page.OutcomeTruncatedAttemptIds.Count
            || page.OutcomeTruncatedAttemptIds.Count > 0 && !page.TextWasTruncated
            || page.HasMore && page.Attempts.Count == 0
            || page.Attempts.Any(item => item == null || item.Id <= 0 || item.RunRecordId != runRecordId || item.NodeId == null
                || continuation != null && item.Id >= continuation.AttemptId
                || nodeId != null && (page.TextWasTruncated ? !nodeId.StartsWith(item.NodeId, StringComparison.Ordinal) : item.NodeId != nodeId))
            || page.Attempts.Zip(page.Attempts.Skip(1), (first, second) => first.Id >= second.Id).Any(invalid => invalid))
            return Failure(CopilotToolFailureKind.Internal, "The attempt source returned inconsistent run, node, order, or page associations; no evidence was published.");
        return Format(page, nodeId, nodeKey, continuation != null);
    }

    private static CopilotCapabilityResult Format(FlowRunAttemptPage page, string? nodeId, string nodeKey, bool isContinuation)
    {
        var textTruncated = page.TextWasTruncated;
        var textRedacted = false;
        string? Text(string? value, int maximum, out bool wasTruncated)
        {
            wasTruncated = false;
            if (value == null) return null;
            var text = CopilotMcpAuditLogger.RedactText(value);
            textRedacted |= text != value;
            if (text.Length <= maximum) return text;
            textTruncated = true;
            wasTruncated = true;
            if (char.IsHighSurrogate(text[maximum - 1]) && char.IsLowSurrogate(text[maximum])) maximum--;
            return text[..maximum];
        }
        var attempts = page.Attempts.Select(item =>
        {
            var started = Utc(item.StartedTimeUtc);
            var completed = item.CompletedTimeUtc.HasValue ? Utc(item.CompletedTimeUtc.Value) : (DateTime?)null;
            var durationValid = item.StartedTimeUtc != default && item.CompletedTimeUtc.HasValue
                && item.CompletedTimeUtc.Value != default && completed!.Value >= started;
            var outcome = Text(item.Outcome, 128, out var outcomeOutputTruncated);
            var outcomeTruncated = outcomeOutputTruncated || page.OutcomeTruncatedAttemptIds.Contains(item.Id);
            return new
            {
                attempt_id = item.Id, run_record_id = item.RunRecordId, node_id = Text(item.NodeId, 128, out _), attempt_no = item.AttemptNo,
                started_time_utc = started, completed_time_utc = completed,
                duration_ms = durationValid ? (double?)(completed!.Value - started).TotalMilliseconds : null, duration_valid = durationValid,
                outcome, outcome_truncated = outcomeTruncated,
                outcome_recognized = !outcomeTruncated && (item.Outcome is "Succeeded" or "Failed" or "TimedOut" or "Canceled" or "Interrupted"),
                error_code = Text(item.ErrorCode, 128, out _), error_message = Text(item.ErrorMessage, 2048, out _),
            };
        }).ToList();
        var displayedNodeId = Text(nodeId, 128, out _);
        var outputLimited = false;
        bool HasMore() => page.HasMore || attempts.Count < page.Attempts.Count;
        string? NextCursor() => !HasMore() || attempts.Count == 0 ? null : Encode(new Cursor(
            page.RunRecordId, page.SourceKey.ToLowerInvariant(), nodeKey, attempts[0].attempt_id));
        string Serialize() => JsonSerializer.Serialize(new
        {
            source = "local_flow_journal_run_attempts", run_record_id = page.RunRecordId, node_id = displayedNodeId, attempts,
            has_more = HasMore(), next_cursor = NextCursor(),
            interpretation = "Exact recorded run and optional node association; no SN, name, or current-canvas fallback. Attempts and repeated attempt_no values are not deduplicated. Outcomes and errors are untrusted recorded text, not proof of run continuation, ContinueOnFail, a root cause, measurements, or customer acceptance. Duration uses valid stored start/end timestamps only. Invocation identifiers, legacy ids, payloads, device control, and reruns are excluded.",
            coverage = new
            {
                attempts_returned = attempts.Count, text_truncated = textTruncated, text_redacted = textRedacted, output_limited = outputLimited,
                page_includes_latest_attempts = !isContinuation, reached_oldest_retained_attempt = !HasMore(),
                unknown_outcome_attempts = attempts.Count(item => !item.outcome_recognized),
                unknown_duration_attempts = attempts.Count(item => !item.duration_valid),
                ordering = "Latest attempt_id DESC window, delivered attempt_id ASC; next_cursor continues to earlier attempt ids. Record id order does not establish execution start order.",
                query_scope = "Each page is one read-only live journal snapshot, not frozen whole-run history. Appends, retention, and updates may change later pages. Missing attempts do not prove a node was not executed; the last page only reaches the current retained lower bound. Redacted or truncated node ids must not be reconstructed or used to infer exact identity.",
            },
        });
        var content = Serialize();
        while (content.Length > MaximumContentCharacters && attempts.Count > 1)
        {
            attempts.RemoveAt(0);
            outputLimited = true;
            content = Serialize();
        }
        if (content.Length > MaximumContentCharacters)
            return Failure(CopilotToolFailureKind.Validation, "The attempt evidence could not fit the bounded output contract; continuation was not advanced.");
        return new CopilotCapabilityResult
        {
            Success = true, Summary = $"Read {attempts.Count} retained attempts for exact run {page.RunRecordId}.", Content = content,
            PartialResultMessage = isContinuation || HasMore() || textTruncated || textRedacted
                || attempts.Any(item => !item.outcome_recognized || !item.duration_valid)
                ? "Only the indicated retained attempt window was read. Inspect unknown values and coverage; follow next_cursor when present."
                : string.Empty,
        };
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
    private static CopilotCapabilityResult InvalidCursor() => Failure(CopilotToolFailureKind.Validation,
        "The run-attempt cursor is invalid, stale, or belongs to another run, exact node filter, or journal. Restart without a cursor.");
    private static CopilotCapabilityResult Failure(CopilotToolFailureKind kind, string message) => new()
    {
        Success = false, FailureKind = kind, Summary = "Run attempts were not inspected.", ErrorMessage = message,
    };
}
