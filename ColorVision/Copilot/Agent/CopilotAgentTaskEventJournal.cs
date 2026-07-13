using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot
{
    public enum CopilotAgentTaskEventType
    {
        RunStarted,
        SessionResumed,
        ReplanRequired,
        TaskLedgerCaptured,
        ToolStarted,
        ToolCompleted,
        ApprovalRequested,
        ApprovalApproved,
        ApprovalDenied,
        SteeringQueued,
        EvidenceCaptured,
        RuntimeError,
        RunStopped,
        RecoveryRequested,
    }

    public sealed class CopilotAgentTaskEvent
    {
        public long Sequence { get; init; }

        public string Id { get; init; } = string.Empty;

        public CopilotAgentTaskEventType Type { get; init; }

        public DateTimeOffset OccurredAtUtc { get; init; }

        public string RunId { get; init; } = string.Empty;

        public string SubjectId { get; init; } = string.Empty;

        public IReadOnlyList<string> RelatedIds { get; init; } = Array.Empty<string>();

        public string ToolName { get; init; } = string.Empty;

        public string State { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public bool IsStructurallyValid()
        {
            return Sequence > 0
                && CopilotAgentTaskEventIds.IsKey(Id, "task-event", 32)
                && Enum.IsDefined(Type)
                && OccurredAtUtc != default
                && CopilotAgentTaskEventIds.IsKey(RunId, "run", 32)
                && IsIdentifier(SubjectId)
                && (RelatedIds?.Count ?? 0) <= CopilotAgentTaskEventJournal.MaxRelatedIds
                && (RelatedIds?.All(IsIdentifier) ?? true)
                && (RelatedIds?.Distinct(StringComparer.Ordinal).Count() == RelatedIds?.Count)
                && IsOptionalBounded(ToolName, CopilotAgentTaskEventJournal.MaxToolNameLength)
                && IsOptionalBounded(State, CopilotAgentTaskEventJournal.MaxStateLength)
                && IsOptionalBounded(Summary, CopilotAgentTaskEventJournal.MaxSummaryLength);
        }

        private static bool IsIdentifier(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Length <= CopilotAgentTaskEventJournal.MaxIdentifierLength
                && value.All(character => char.IsLetterOrDigit(character) || character is ':' or '-' or '_' or '.');
        }

        private static bool IsOptionalBounded(string? value, int maximumLength)
        {
            return value != null && value.Length <= maximumLength && value.All(character => !char.IsControl(character));
        }
    }

    public sealed class CopilotAgentTaskEventJournalSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; init; } = CurrentSchemaVersion;

        public IReadOnlyList<CopilotAgentTaskEvent> Events { get; init; } = Array.Empty<CopilotAgentTaskEvent>();

        public bool IsStructurallyValid()
        {
            if (SchemaVersion != CurrentSchemaVersion
                || Events == null
                || Events.Count > CopilotAgentTaskEventJournal.MaxEvents
                || Events.Any(item => item?.IsStructurallyValid() != true)
                || Events.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != Events.Count
                || Events.Select(item => item.Sequence).Distinct().Count() != Events.Count)
            {
                return false;
            }

            return Events.Zip(Events.Skip(1), (left, right) => left.Sequence < right.Sequence).All(value => value);
        }
    }

    public sealed class CopilotAgentTaskEventQuery
    {
        public IReadOnlyCollection<CopilotAgentTaskEventType> Types { get; init; } = Array.Empty<CopilotAgentTaskEventType>();

        public string RunId { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public string SubjectOrRelatedId { get; init; } = string.Empty;

        public long BeforeSequence { get; init; } = long.MaxValue;

        public int Limit { get; init; } = 50;
    }

    public sealed class CopilotAgentTaskEventQueryResult
    {
        public IReadOnlyList<CopilotAgentTaskEvent> Events { get; init; } = Array.Empty<CopilotAgentTaskEvent>();

        public bool HasMore { get; init; }

        public long? NextBeforeSequence { get; init; }
    }

    public static class CopilotAgentTaskEventIds
    {
        public static string CreateRunId()
        {
            return "run:" + Guid.NewGuid().ToString("N");
        }

        public static string ForCall(string? callId)
        {
            return CreateHashedKey("call", callId);
        }

        public static string ForApproval(string? actionId)
        {
            return CreateHashedKey("approval", actionId);
        }

        public static string ForSteering(string? message)
        {
            return CreateHashedKey("steering", message);
        }

        internal static string CreateEventId(long sequence, string runId, CopilotAgentTaskEventType type, DateTimeOffset occurredAtUtc)
        {
            return CreateHashedKey("task-event", $"{sequence}|{runId}|{(int)type}|{occurredAtUtc:O}");
        }

        internal static bool IsKey(string? value, string prefix, int suffixLength)
        {
            var expectedPrefix = prefix + ":";
            return value?.Length == expectedPrefix.Length + suffixLength
                && value.StartsWith(expectedPrefix, StringComparison.Ordinal)
                && value[expectedPrefix.Length..].All(Uri.IsHexDigit);
        }

        private static string CreateHashedKey(string prefix, string? value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return prefix + ":" + Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
        }
    }

    public static class CopilotAgentTaskEventJournal
    {
        public const int MaxEvents = 256;
        public const int MaxRelatedIds = 16;
        public const int MaxIdentifierLength = 160;
        public const int MaxToolNameLength = 120;
        public const int MaxStateLength = 80;
        public const int MaxSummaryLength = 320;
        public const int MaxQueryLimit = 100;

        public static CopilotAgentTaskEventQueryResult Query(
            CopilotAgentTaskEventJournalSnapshot? snapshot,
            CopilotAgentTaskEventQuery? query = null)
        {
            if (snapshot?.IsStructurallyValid() != true)
                return new CopilotAgentTaskEventQueryResult();

            query ??= new CopilotAgentTaskEventQuery();
            var types = (query.Types ?? Array.Empty<CopilotAgentTaskEventType>())
                .Where(Enum.IsDefined)
                .ToHashSet();
            var beforeSequence = Math.Max(0, query.BeforeSequence);
            var limit = Math.Clamp(query.Limit, 1, MaxQueryLimit);
            var matches = snapshot.Events
                .Where(item => item.Sequence < beforeSequence)
                .Where(item => types.Count == 0 || types.Contains(item.Type))
                .Where(item => string.IsNullOrWhiteSpace(query.RunId) || string.Equals(item.RunId, query.RunId.Trim(), StringComparison.Ordinal))
                .Where(item => string.IsNullOrWhiteSpace(query.ToolName) || string.Equals(item.ToolName, query.ToolName.Trim(), StringComparison.OrdinalIgnoreCase))
                .Where(item => string.IsNullOrWhiteSpace(query.SubjectOrRelatedId)
                    || string.Equals(item.SubjectId, query.SubjectOrRelatedId.Trim(), StringComparison.Ordinal)
                    || item.RelatedIds.Contains(query.SubjectOrRelatedId.Trim(), StringComparer.Ordinal))
                .OrderByDescending(item => item.Sequence)
                .Take(limit + 1)
                .ToArray();
            var hasMore = matches.Length > limit;
            var page = matches.Take(limit).ToArray();
            return new CopilotAgentTaskEventQueryResult
            {
                Events = page,
                HasMore = hasMore,
                NextBeforeSequence = hasMore && page.Length > 0 ? page[^1].Sequence : null,
            };
        }
    }

    public sealed class CopilotAgentTaskEventJournalBuilder
    {
        private readonly object _syncRoot = new();
        private readonly List<CopilotAgentTaskEvent> _events = new();
        private long _nextSequence;

        public CopilotAgentTaskEventJournalBuilder(CopilotAgentTaskEventJournalSnapshot? previous = null)
        {
            if (previous?.IsStructurallyValid() == true)
                _events.AddRange(previous.Events.TakeLast(CopilotAgentTaskEventJournal.MaxEvents));
            _nextSequence = _events.Count == 0 ? 1 : _events.Max(item => item.Sequence) + 1;
            RunId = CopilotAgentTaskEventIds.CreateRunId();
        }

        public string RunId { get; }

        public void RecordRunStarted()
        {
            Append(CopilotAgentTaskEventType.RunStarted, RunId, "running", "Agent run started.");
        }

        public void RecordRecovery(CopilotAgentRecoveryRequest recovery)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            if (!recovery.IsStructurallyValid())
                throw new ArgumentException("Agent recovery request is not structurally valid.", nameof(recovery));

            var subjectId = recovery.Mode == CopilotAgentRecoveryMode.RetryRead ? recovery.SourceCallKey : RunId;
            Append(
                CopilotAgentTaskEventType.RecoveryRequested,
                subjectId,
                recovery.Mode.ToString(),
                recovery.Mode switch
                {
                    CopilotAgentRecoveryMode.RetryRead => "Recovery may re-evaluate one retry-eligible read failure without replaying stored arguments.",
                    CopilotAgentRecoveryMode.Replan => "Recovery requires a fresh plan against current capabilities.",
                    _ => "Recovery resumes the incomplete task ledger from its checkpoint.",
                },
                recovery.ToolName);
        }

        public void RecordSessionResumed()
        {
            Append(CopilotAgentTaskEventType.SessionResumed, RunId, "resumed", "Agent session and task state resumed from checkpoint.");
        }

        public void RecordReplanRequired(CopilotAgentCheckpointCompatibilityKind reason)
        {
            Append(CopilotAgentTaskEventType.ReplanRequired, RunId, reason.ToString(), "Persisted task state was discarded and must be replanned.");
        }

        public void RecordTaskLedger(CopilotAgentTaskLedgerSnapshot ledger, string phase)
        {
            ArgumentNullException.ThrowIfNull(ledger);
            var items = ledger.Items ?? Array.Empty<CopilotAgentTaskItem>();
            var completedCount = items.Count(item => item?.IsComplete == true);
            var relatedIds = items.Where(item => item != null).Select(item => $"task:{Math.Max(0, item.Id)}");
            Append(
                CopilotAgentTaskEventType.TaskLedgerCaptured,
                RunId,
                phase,
                $"Task ledger {completedCount}/{items.Count} complete in {ledger.Mode} mode.",
                relatedIds: relatedIds);
        }

        public void RecordApprovalDecision(CopilotToolExecutionInfo execution, bool approved)
        {
            ArgumentNullException.ThrowIfNull(execution);
            RecordApprovalDecision(execution.ToolName, execution.CallId, execution.ApprovalActionId, approved);
        }

        public void RecordApprovalDecision(string toolName, string callId, string approvalActionId, bool approved)
        {
            var approvalId = CopilotAgentTaskEventIds.ForApproval(approvalActionId);
            Append(
                approved ? CopilotAgentTaskEventType.ApprovalApproved : CopilotAgentTaskEventType.ApprovalDenied,
                approvalId,
                approved ? "approved" : "denied",
                approved ? "Protected tool call was approved." : "Protected tool call was denied or expired.",
                toolName,
                [CopilotAgentTaskEventIds.ForCall(callId)]);
        }

        public void RecordSteering(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Steering message cannot be empty.", nameof(message));
            Append(
                CopilotAgentTaskEventType.SteeringQueued,
                CopilotAgentTaskEventIds.ForSteering(message),
                "queued",
                "A user steering instruction was queued for the active Agent session.");
        }

        public void RecordEvidence(CopilotAgentEvidenceArtifact artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            if (!artifact.IsStructurallyValid())
                throw new ArgumentException("Evidence artifact is not structurally valid.", nameof(artifact));
            var related = new[] { artifact.SourceCallKey, artifact.ResourceKey }
                .Where(value => !string.IsNullOrWhiteSpace(value));
            Append(
                CopilotAgentTaskEventType.EvidenceCaptured,
                artifact.Id,
                "captured",
                artifact.Summary,
                artifact.ToolName,
                related,
                artifact.CapturedAtUtc);
        }

        public void RecordStop(CopilotAgentStopReason reason)
        {
            Append(CopilotAgentTaskEventType.RunStopped, RunId, reason.ToString(), $"Agent run stopped with reason {reason}.");
        }

        public void Observe(CopilotAgentEvent agentEvent)
        {
            if (agentEvent == null)
                return;

            var execution = agentEvent.ToolExecution;
            if (agentEvent.Type == CopilotAgentEventType.ToolStarted && execution != null)
            {
                Append(
                    CopilotAgentTaskEventType.ToolStarted,
                    CopilotAgentTaskEventIds.ForCall(execution.CallId),
                    execution.State.ToString(),
                    "Tool execution started.",
                    execution.ToolName,
                    occurredAtUtc: execution.StartedAtUtc == default ? null : execution.StartedAtUtc);
                return;
            }

            if (agentEvent.Type == CopilotAgentEventType.ToolResult && execution != null)
            {
                var type = execution.State switch
                {
                    CopilotToolExecutionState.AwaitingApproval => CopilotAgentTaskEventType.ApprovalRequested,
                    CopilotToolExecutionState.Denied => CopilotAgentTaskEventType.ApprovalDenied,
                    _ => CopilotAgentTaskEventType.ToolCompleted,
                };
                var callId = CopilotAgentTaskEventIds.ForCall(execution.CallId);
                var subjectId = type is CopilotAgentTaskEventType.ApprovalRequested or CopilotAgentTaskEventType.ApprovalDenied
                    ? CopilotAgentTaskEventIds.ForApproval(execution.ApprovalActionId)
                    : callId;
                var related = subjectId == callId ? Array.Empty<string>() : [callId];
                Append(
                    type,
                    subjectId,
                    execution.State.ToString(),
                    agentEvent.ToolResult?.Summary ?? agentEvent.Text,
                    execution.ToolName,
                    related,
                    execution.CompletedAtUtc ?? (execution.StartedAtUtc == default ? null : execution.StartedAtUtc));
                return;
            }

            if (agentEvent.Type == CopilotAgentEventType.Error)
                Append(CopilotAgentTaskEventType.RuntimeError, RunId, "error", agentEvent.Text);
        }

        public CopilotAgentTaskEventJournalSnapshot Snapshot()
        {
            lock (_syncRoot)
            {
                return new CopilotAgentTaskEventJournalSnapshot
                {
                    Events = _events.ToArray(),
                };
            }
        }

        private void Append(
            CopilotAgentTaskEventType type,
            string subjectId,
            string state,
            string summary,
            string toolName = "",
            IEnumerable<string>? relatedIds = null,
            DateTimeOffset? occurredAtUtc = null)
        {
            lock (_syncRoot)
            {
                var timestamp = occurredAtUtc ?? DateTimeOffset.UtcNow;
                var sequence = _nextSequence++;
                var item = new CopilotAgentTaskEvent
                {
                    Sequence = sequence,
                    Id = CopilotAgentTaskEventIds.CreateEventId(sequence, RunId, type, timestamp),
                    Type = type,
                    OccurredAtUtc = timestamp,
                    RunId = RunId,
                    SubjectId = NormalizeIdentifier(subjectId, RunId),
                    RelatedIds = (relatedIds ?? Array.Empty<string>())
                        .Select(value => NormalizeIdentifier(value, string.Empty))
                        .Where(value => value.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .Take(CopilotAgentTaskEventJournal.MaxRelatedIds)
                        .ToArray(),
                    ToolName = SanitizeText(toolName, CopilotAgentTaskEventJournal.MaxToolNameLength, collapseWhitespace: true),
                    State = SanitizeText(state, CopilotAgentTaskEventJournal.MaxStateLength, collapseWhitespace: true),
                    Summary = SanitizeText(summary, CopilotAgentTaskEventJournal.MaxSummaryLength, collapseWhitespace: true),
                };
                if (!item.IsStructurallyValid())
                    throw new InvalidOperationException("Agent task event could not be normalized into a valid journal entry.");
                _events.Add(item);
                if (_events.Count > CopilotAgentTaskEventJournal.MaxEvents)
                    _events.RemoveRange(0, _events.Count - CopilotAgentTaskEventJournal.MaxEvents);
            }
        }

        private static string NormalizeIdentifier(string? value, string fallback)
        {
            var normalized = new string((value ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character is ':' or '-' or '_' or '.')
                .Take(CopilotAgentTaskEventJournal.MaxIdentifierLength)
                .ToArray());
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        private static string SanitizeText(string? value, int maximumLength, bool collapseWhitespace)
        {
            var sanitized = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Replace("\0", string.Empty, StringComparison.Ordinal);
            if (collapseWhitespace)
                sanitized = string.Join(" ", sanitized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (sanitized.Length <= maximumLength)
                return sanitized;
            return maximumLength <= 3 ? sanitized[..maximumLength] : sanitized[..(maximumLength - 3)] + "...";
        }
    }
}
