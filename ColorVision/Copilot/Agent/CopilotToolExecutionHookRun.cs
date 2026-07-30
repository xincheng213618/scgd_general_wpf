using System;
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
    }

    public sealed class CopilotToolExecutionHookRun
    {
        public const int MaxSourceIdLength = CopilotToolExecutionHookRegistry.MaxSourceIdLength;
        public const long MaxDurationMs = 86_400_000;

        public string SourceId { get; init; } = string.Empty;

        public CopilotToolExecutionHookPhase Phase { get; init; }

        public CopilotToolExecutionHookState State { get; init; }

        public long DurationMs { get; init; }

        public string FailureCode { get; init; } = string.Empty;

        public bool IsStructurallyValid()
        {
            var normalizedFailureCode = CopilotToolFailureCode.Normalize(FailureCode);
            return !string.IsNullOrWhiteSpace(SourceId)
                && string.Equals(SourceId, SourceId.Trim(), StringComparison.Ordinal)
                && SourceId.Length <= MaxSourceIdLength
                && !SourceId.Any(char.IsControl)
                && Enum.IsDefined(Phase)
                && Enum.IsDefined(State)
                && DurationMs is >= 0 and <= MaxDurationMs
                && string.Equals(FailureCode, normalizedFailureCode, StringComparison.Ordinal)
                && (State == CopilotToolExecutionHookState.Completed
                    ? normalizedFailureCode.Length == 0
                    : normalizedFailureCode.Length > 0);
        }

        internal static CopilotToolExecutionHookRun Create(
            string sourceId,
            CopilotToolExecutionHookPhase phase,
            CopilotToolExecutionHookState state,
            long durationMs,
            string failureCode = "")
        {
            var normalizedFailureCode = state == CopilotToolExecutionHookState.Completed
                ? string.Empty
                : CopilotToolFailureCode.Normalize(failureCode);
            return new CopilotToolExecutionHookRun
            {
                SourceId = NormalizeSourceId(sourceId),
                Phase = phase,
                State = state,
                DurationMs = Math.Clamp(durationMs, 0, MaxDurationMs),
                FailureCode = normalizedFailureCode,
            };
        }

        private static string NormalizeSourceId(string? sourceId)
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
}
