using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotToolExecutionHookSkippedException : Exception
    {
        public CopilotToolExecutionHookSkippedException(
            string failureCode,
            string message)
            : base(message)
        {
            FailureCode = string.IsNullOrWhiteSpace(CopilotToolFailureCode.Normalize(failureCode))
                ? "tool_hook_skipped"
                : CopilotToolFailureCode.Normalize(failureCode);
        }

        public string FailureCode { get; }
    }

    public enum CopilotToolExecutionHookPhase
    {
        BeforeExecute,
        AfterExecute,
        PermissionRequest,
    }

    public enum CopilotToolExecutionHookState
    {
        Completed,
        Denied,
        Failed,
        TimedOut,
        Cancelled,
        Skipped,
        Scheduled,
        Blocked,
        Stopped,
    }

    public sealed class CopilotToolExecutionHookRun
    {
        public const int MaxSourceIdLength = CopilotToolExecutionHookRegistry.MaxSourceIdLength;
        public const long MaxDurationMs = 86_400_000;

        public string SourceId { get; init; } = string.Empty;

        public CopilotToolExecutionHookPhase Phase { get; init; }

        public CopilotToolExecutionHookMode ExecutionMode { get; init; } =
            CopilotToolExecutionHookMode.Sync;

        public CopilotToolExecutionHookState State { get; init; }

        public long DurationMs { get; init; }

        public string FailureCode { get; init; } = string.Empty;

        public bool ShouldSerializeExecutionMode() =>
            ExecutionMode != CopilotToolExecutionHookMode.Sync;

        public bool IsStructurallyValid()
        {
            var normalizedFailureCode = CopilotToolFailureCode.Normalize(FailureCode);
            return !string.IsNullOrWhiteSpace(SourceId)
                && string.Equals(SourceId, SourceId.Trim(), StringComparison.Ordinal)
                && SourceId.Length <= MaxSourceIdLength
                && !SourceId.Any(char.IsControl)
                && Enum.IsDefined(Phase)
                && Enum.IsDefined(ExecutionMode)
                && Enum.IsDefined(State)
                && (State != CopilotToolExecutionHookState.Scheduled
                    || ExecutionMode == CopilotToolExecutionHookMode.Async)
                && DurationMs is >= 0 and <= MaxDurationMs
                && string.Equals(FailureCode, normalizedFailureCode, StringComparison.Ordinal)
                && (State is CopilotToolExecutionHookState.Completed
                    or CopilotToolExecutionHookState.Scheduled
                    or CopilotToolExecutionHookState.Blocked
                    or CopilotToolExecutionHookState.Stopped
                    ? normalizedFailureCode.Length == 0
                    : normalizedFailureCode.Length > 0);
        }

        internal static CopilotToolExecutionHookRun Create(
            string sourceId,
            CopilotToolExecutionHookPhase phase,
            CopilotToolExecutionHookState state,
            long durationMs,
            string failureCode = "",
            CopilotToolExecutionHookMode executionMode = CopilotToolExecutionHookMode.Sync)
        {
            var normalizedFailureCode = state is CopilotToolExecutionHookState.Completed
                    or CopilotToolExecutionHookState.Scheduled
                    or CopilotToolExecutionHookState.Blocked
                    or CopilotToolExecutionHookState.Stopped
                ? string.Empty
                : CopilotToolFailureCode.Normalize(failureCode);
            return new CopilotToolExecutionHookRun
            {
                SourceId = NormalizeSourceId(sourceId),
                Phase = phase,
                ExecutionMode = executionMode,
                State = state,
                DurationMs = Math.Clamp(durationMs, 0, MaxDurationMs),
                FailureCode = normalizedFailureCode,
            };
        }

        internal CopilotToolExecutionHookRun CreateSnapshot() => Create(
            SourceId,
            Phase,
            State,
            DurationMs,
            FailureCode,
            ExecutionMode);

        internal static string NormalizeSourceId(string? sourceId)
        {
            var normalized = (sourceId ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            return normalized.Length <= MaxSourceIdLength
                ? normalized
                : normalized[..MaxSourceIdLength];
        }
    }

    internal static class CopilotToolExecutionHookRunProtocol
    {
        internal const int MaximumEntries = (CopilotToolExecutionHookRegistry.MaxRegistrations + 1) * 3;

        internal static bool IsStructurallyValid(
            IReadOnlyList<CopilotToolExecutionHookRun>? runs)
        {
            if (runs == null || runs.Count > MaximumEntries)
                return false;

            return runs.All(run => run?.IsStructurallyValid() == true)
                && runs
                    .Select(run => (run.SourceId, run.Phase))
                    .Distinct()
                    .Count() == runs.Count;
        }
    }

    public sealed class CopilotToolExecutionHookLifecycle
    {
        public string SourceId { get; init; } = string.Empty;

        public CopilotToolExecutionHookPhase Phase { get; init; }

        public CopilotToolExecutionHookRun? Result { get; init; }

        public bool IsCompleted => Result != null;

        public bool IsStructurallyValid(bool requireCompleted)
        {
            if (string.IsNullOrWhiteSpace(SourceId)
                || !string.Equals(SourceId, CopilotToolExecutionHookRun.NormalizeSourceId(SourceId), StringComparison.Ordinal)
                || SourceId.Any(char.IsControl)
                || !Enum.IsDefined(Phase)
                || IsCompleted != requireCompleted)
            {
                return false;
            }

            return Result == null
                || (Result.IsStructurallyValid()
                    && string.Equals(Result.SourceId, SourceId, StringComparison.Ordinal)
                    && Result.Phase == Phase);
        }

        internal static CopilotToolExecutionHookLifecycle Started(
            string sourceId,
            CopilotToolExecutionHookPhase phase)
        {
            return new CopilotToolExecutionHookLifecycle
            {
                SourceId = CopilotToolExecutionHookRun.NormalizeSourceId(sourceId),
                Phase = phase,
            };
        }

        internal static CopilotToolExecutionHookLifecycle Completed(
            CopilotToolExecutionHookRun result)
        {
            ArgumentNullException.ThrowIfNull(result);
            var snapshot = result.CreateSnapshot();
            return new CopilotToolExecutionHookLifecycle
            {
                SourceId = snapshot.SourceId,
                Phase = snapshot.Phase,
                Result = snapshot,
            };
        }
    }
}
