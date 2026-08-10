using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotToolInvocation
    {
        private readonly List<CopilotToolAdditionalContext> _preToolAdditionalContexts = [];

        public string CallId { get; init; } = string.Empty;

        public int Round { get; init; }

        public int Attempt { get; init; } = 1;

        public int MaxAttempts { get; init; } = 1;

        public string RuntimeName { get; init; } = string.Empty;

        public ICopilotTool Tool { get; init; } = null!;

        public CopilotAgentRequest AgentRequest { get; init; } = null!;

        internal CopilotExecutionScope ExecutionScope { get; init; } = CopilotExecutionScope.Empty;

        public CopilotAgentToolInput ToolInput { get; init; } = CopilotAgentToolInput.Empty;

        public CopilotToolCall ToolCall { get; init; } = new();

        public bool FrameworkApprovalGranted { get; internal init; }

        public string ApprovalActionId { get; internal init; } = string.Empty;

        internal CopilotApprovalPromptCategory? ApprovalPromptCategoryOverride { get; init; }

        internal string ApprovalPromptReasonOverride { get; init; } = string.Empty;

        internal CopilotApprovalPromptCategory EffectiveApprovalPromptCategory =>
            ApprovalPromptCategoryOverride ?? Tool.Capability.ApprovalPromptCategory;

        public CopilotToolConcurrencyMode ConcurrencyMode { get; internal init; }

        public string ConcurrencyKey { get; internal init; } = string.Empty;

        internal string PreviousObservationProgressSignature { get; init; } =
            string.Empty;

        internal IReadOnlyList<CopilotToolExecutionHookRun> InitialHookRuns { get; init; } =
            Array.Empty<CopilotToolExecutionHookRun>();

        internal IReadOnlyList<CopilotToolExecutionHookBinding> InitialHookBindings { get; init; } =
            Array.Empty<CopilotToolExecutionHookBinding>();

        internal IReadOnlyList<CopilotToolAdditionalContext> PreToolAdditionalContexts
        {
            get
            {
                lock (_preToolAdditionalContexts)
                    return _preToolAdditionalContexts.ToArray();
            }
        }

        internal void AddPreToolAdditionalContext(string? context, int maximumTokens)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maximumTokens);
            if (string.IsNullOrWhiteSpace(context))
                return;

            lock (_preToolAdditionalContexts)
            {
                _preToolAdditionalContexts.Add(new CopilotToolAdditionalContext(
                    context,
                    maximumTokens));
            }
        }
    }

    internal sealed record CopilotToolAdditionalContext(string Text, int MaximumTokens);

    internal static class CopilotToolInvocationContext
    {
        private static readonly AsyncLocal<CopilotToolInvocation?> CurrentInvocation = new();

        public static CopilotToolInvocation? Current => CurrentInvocation.Value;

        public static IDisposable Enter(CopilotToolInvocation invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            var previous = CurrentInvocation.Value;
            CurrentInvocation.Value = invocation;
            return new Scope(previous);
        }

        private sealed class Scope(CopilotToolInvocation? previous) : IDisposable
        {
            private CopilotToolInvocation? _previous = previous;
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;

                CurrentInvocation.Value = _previous;
                _previous = null;
            }
        }
    }

    public sealed class CopilotToolExecutionOutcome
    {
        internal const int DefaultAdditionalContextLimitTokens = 2_500;
        private const int MaximumModelFeedbackCharacters = 12_000;
        private const string PostToolAdditionalContextTruncationMarker =
            "\n...[PostToolUse additional context truncated]...\n";
        private const string PreToolAdditionalContextTruncationMarker =
            "\n...[PreToolUse additional context truncated]...\n";
        private CopilotToolResult? _modelVisibleResult;
        private readonly List<string> _modelAdditionalContexts = [];

        internal string? FormattedModelResult { get; set; }

        public CopilotToolInvocation Invocation { get; init; } = null!;

        public CopilotToolResult Result { get; init; } = new();

        public CopilotToolExecutionInfo Execution { get; init; } = new();

        public IReadOnlyList<CopilotToolExecutionHookRun> HookRuns { get; internal set; } =
            Array.Empty<CopilotToolExecutionHookRun>();

        internal CopilotToolResult EffectiveModelResult => _modelVisibleResult ?? Result;

        internal IReadOnlyList<string> ModelAdditionalContexts
        {
            get
            {
                lock (_modelAdditionalContexts)
                    return _modelAdditionalContexts.ToArray();
            }
        }

        public CopilotAgentStepRecord StepRecord => new()
        {
            Round = Invocation.Round,
            ToolCall = Invocation.ToolCall,
            Observation = CopilotToolObservation.FromResult(Result),
            ModelObservation = _modelVisibleResult == null
                ? null
                : CopilotToolObservation.FromResult(_modelVisibleResult),
            ModelToolResult = FormattedModelResult ?? string.Empty,
            Execution = Execution,
            SuppressModelOutput = EffectiveModelResult.SuppressModelOutput,
        };

        internal void ApplyModelVisibleFeedback(string? message)
        {
            var feedback = CopilotMcpAuditLogger.RedactText(message).Trim();
            if (feedback.Length == 0)
                return;
            feedback = BoundModelFeedback(feedback);

            if (_modelVisibleResult != null)
                feedback = BoundModelFeedback(_modelVisibleResult.Content + Environment.NewLine + feedback);

            // PostToolUse feedback changes only what the model observes. Keep the
            // operational result intact for audit, approval, retry, rollback, and usage accounting.
            var original = Result;
            _modelVisibleResult = new CopilotToolResult
            {
                ToolName = original.ToolName,
                Success = original.Success,
                Summary = "PostToolUse hook feedback.",
                Content = feedback,
                FailureKind = original.FailureKind,
                FailureCode = original.FailureCode,
                ProcessOperation = original.ProcessOperation,
                ProcessExitCode = original.ProcessExitCode,
                ProcessTimedOut = original.ProcessTimedOut,
                SuppressModelOutput = false,
            };
        }

        internal void AddModelAdditionalContext(
            string? context,
            int maximumTokens = DefaultAdditionalContextLimitTokens,
            bool isPreToolUse = false)
        {
            var bounded = NormalizeModelAdditionalContext(
                context,
                maximumTokens,
                isPreToolUse
                    ? PreToolAdditionalContextTruncationMarker
                    : PostToolAdditionalContextTruncationMarker);
            if (bounded.Length == 0)
                return;
            lock (_modelAdditionalContexts)
                _modelAdditionalContexts.Add(bounded);
        }

        internal static string NormalizeModelAdditionalContext(
            string? context,
            int maximumTokens,
            string truncationMarker)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maximumTokens);
            ArgumentException.ThrowIfNullOrWhiteSpace(truncationMarker);
            var normalized = CopilotMcpAuditLogger.RedactText(context).Trim();
            if (normalized.Length == 0 || maximumTokens == 0)
                return normalized;
            return BoundAdditionalContext(normalized, maximumTokens, truncationMarker);
        }

        private static string BoundModelFeedback(string value)
        {
            if (value.Length <= MaximumModelFeedbackCharacters)
                return value;

            var length = MaximumModelFeedbackCharacters;
            if (char.IsHighSurrogate(value[length - 1]))
                length--;
            return value[..length];
        }

        private static string BoundAdditionalContext(
            string value,
            int maximumTokens,
            string truncationMarker)
        {
            var maximumWeight = (long)maximumTokens
                * CopilotTokenEstimator.AsciiCharactersPerToken;
            if (CopilotTokenEstimator.EstimateTextWeight(value) <= maximumWeight)
                return value;

            var markerWeight = CopilotTokenEstimator.EstimateTextWeight(
                truncationMarker);
            if (markerWeight >= maximumWeight)
            {
                return value[..CopilotTokenEstimator.GetPrefixLengthWithinWeight(
                    value,
                    maximumWeight)];
            }
            var previewWeight = maximumWeight
                - markerWeight;
            var headWeight = Math.Max(1, (previewWeight + 1) / 2);
            var tailWeight = Math.Max(1, previewWeight - headWeight);
            var headLength = CopilotTokenEstimator.GetPrefixLengthWithinWeight(value, headWeight);
            long retainedTailWeight = 0;
            var tailStart = value.Length;
            while (tailStart > headLength)
            {
                var characterWeight = value[tailStart - 1] <= 0x7f
                    ? 1
                    : CopilotTokenEstimator.AsciiCharactersPerToken;
                if (retainedTailWeight + characterWeight > tailWeight)
                    break;
                retainedTailWeight += characterWeight;
                tailStart--;
            }
            if (tailStart < value.Length
                && char.IsLowSurrogate(value[tailStart])
                && tailStart > 0
                && char.IsHighSurrogate(value[tailStart - 1]))
            {
                tailStart++;
            }
            return value[..headLength]
                + truncationMarker
                + value[tailStart..];
        }
    }

    public sealed class CopilotToolExecutionHookContext
    {
        public CopilotToolInvocation Invocation { get; init; } = null!;

        public DateTimeOffset StartedAtUtc { get; init; }

        public TimeSpan Timeout { get; init; }
    }

    public sealed class CopilotToolExecutionHookDecision
    {
        public static CopilotToolExecutionHookDecision Proceed { get; } = new() { ShouldProceed = true };

        public bool ShouldProceed { get; init; }

        public string Reason { get; init; } = string.Empty;

        public CopilotToolFailureKind FailureKind { get; init; }

        public string FailureCode { get; init; } = string.Empty;

        public static CopilotToolExecutionHookDecision Deny(
            string reason,
            string failureCode = "tool_hook_denied",
            CopilotToolFailureKind failureKind = CopilotToolFailureKind.Authorization)
        {
            var normalizedFailureCode = CopilotToolFailureCode.Normalize(failureCode);
            return new CopilotToolExecutionHookDecision
            {
                ShouldProceed = false,
                Reason = reason ?? string.Empty,
                FailureKind = failureKind != CopilotToolFailureKind.None && Enum.IsDefined(failureKind)
                    ? failureKind
                    : CopilotToolFailureKind.Authorization,
                FailureCode = string.IsNullOrWhiteSpace(normalizedFailureCode)
                    ? "tool_hook_denied"
                    : normalizedFailureCode,
            };
        }
    }

    public sealed class CopilotToolPermissionRequestContext
    {
        public CopilotToolInvocation Invocation { get; init; } = null!;

        public DateTimeOffset RequestedAtUtc { get; init; }
    }

    public sealed class CopilotToolPermissionRequestDecision
    {
        public static CopilotToolPermissionRequestDecision Prompt { get; } = new()
        {
            ShouldPrompt = true,
        };

        public bool ShouldPrompt { get; init; }

        public string Reason { get; init; } = string.Empty;

        public string FailureCode { get; init; } = string.Empty;

        public static CopilotToolPermissionRequestDecision PromptWithReason(string reason) => new()
        {
            ShouldPrompt = true,
            Reason = reason ?? string.Empty,
        };

        public static CopilotToolPermissionRequestDecision Deny(
            string reason,
            string failureCode = "permission_hook_denied")
        {
            var normalizedFailureCode = CopilotToolFailureCode.Normalize(failureCode);
            return new CopilotToolPermissionRequestDecision
            {
                ShouldPrompt = false,
                Reason = reason ?? string.Empty,
                FailureCode = string.IsNullOrWhiteSpace(normalizedFailureCode)
                    ? "permission_hook_denied"
                    : normalizedFailureCode,
            };
        }
    }

    internal sealed record CopilotToolPermissionRequestOutput(
        CopilotToolPermissionRequestDecision Decision,
        string SystemMessage = "")
    {
        public bool HasOutput => !Decision.ShouldPrompt
            || !string.IsNullOrWhiteSpace(Decision.Reason)
            || !string.IsNullOrWhiteSpace(SystemMessage);
    }

    internal interface ICopilotToolPermissionRequestOutputHook
    {
        Task<CopilotToolPermissionRequestOutput?> OnPermissionRequestWithOutputAsync(
            CopilotToolPermissionRequestContext context,
            CancellationToken cancellationToken);
    }

    internal static class CopilotApprovalRequestReason
    {
        internal const int MaximumCharacters = 2_048;
        private const string TruncationMarker = "\n...[approval reason truncated]...\n";

        public static string Combine(string? first, string? second)
        {
            var boundedFirst = Bound((first ?? string.Empty).Trim());
            var boundedSecond = Bound((second ?? string.Empty).Trim());
            if (boundedFirst.Length == 0)
                return boundedSecond;
            if (boundedSecond.Length == 0
                || string.Equals(boundedFirst, boundedSecond, StringComparison.Ordinal))
            {
                return boundedFirst;
            }

            return Bound(boundedFirst + Environment.NewLine + boundedSecond);
        }

        public static string Normalize(string? value)
        {
            var redacted = CopilotMcpAuditLogger.RedactText(value ?? string.Empty);
            var encoded = CopilotApprovalReviewTextEncoder.Encode(redacted).Trim();
            return Bound(encoded);
        }

        private static string Bound(string value)
        {
            if (value.Length <= MaximumCharacters)
                return value;

            var available = MaximumCharacters - TruncationMarker.Length;
            var headLength = (available + 1) / 2;
            var tailLength = available - headLength;
            if (headLength > 0 && char.IsHighSurrogate(value[headLength - 1]))
                headLength--;
            var tailStart = value.Length - tailLength;
            if (tailStart < value.Length && char.IsLowSurrogate(value[tailStart]))
                tailStart++;
            return value[..headLength] + TruncationMarker + value[tailStart..];
        }
    }

    public interface ICopilotToolExecutionHook
    {
        Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(CopilotToolExecutionHookContext context, CancellationToken cancellationToken);

        Task AfterExecuteAsync(CopilotToolExecutionOutcome outcome, CancellationToken cancellationToken);
    }

    internal enum CopilotToolPostExecutionControl
    {
        None,
        Blocked,
        Stopped,
    }

    internal sealed record CopilotToolPreExecutionOutput(
        CopilotToolExecutionHookDecision Decision,
        string SystemMessage = "",
        string AdditionalContext = "",
        int AdditionalContextLimitTokens = CopilotToolExecutionOutcome.DefaultAdditionalContextLimitTokens)
    {
        public bool HasOutput => !Decision.ShouldProceed
            || !string.IsNullOrWhiteSpace(SystemMessage)
            || !string.IsNullOrWhiteSpace(AdditionalContext);
    }

    internal interface ICopilotToolPreExecutionOutputHook
    {
        Task<CopilotToolPreExecutionOutput?> BeforeExecuteWithOutputAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken);
    }

    internal sealed record CopilotToolPostExecutionOutput(
        string FeedbackMessage = "",
        string SystemMessage = "",
        string AdditionalContext = "",
        CopilotToolPostExecutionControl Control = CopilotToolPostExecutionControl.None,
        string FailureMessage = "",
        int AdditionalContextLimitTokens = CopilotToolExecutionOutcome.DefaultAdditionalContextLimitTokens)
    {
        public bool HasFailure => !string.IsNullOrWhiteSpace(FailureMessage);

        public bool HasOutput => HasFailure
            || !string.IsNullOrWhiteSpace(FeedbackMessage)
            || !string.IsNullOrWhiteSpace(SystemMessage)
            || !string.IsNullOrWhiteSpace(AdditionalContext)
            || Control != CopilotToolPostExecutionControl.None;
    }

    internal interface ICopilotToolPostExecutionOutputHook
    {
        Task<CopilotToolPostExecutionOutput?> AfterExecuteWithOutputAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Inspects a protected call before ColorVision creates a native approval
    /// request. The hook can keep the prompt or deny the call; it cannot grant
    /// framework approval.
    /// </summary>
    public interface ICopilotToolPermissionRequestHook : ICopilotToolExecutionHook
    {
        Task<CopilotToolPermissionRequestDecision> OnPermissionRequestAsync(
            CopilotToolPermissionRequestContext context,
            CancellationToken cancellationToken);
    }

    internal sealed class CopilotToolPermissionRequestOutcome
    {
        public CopilotToolPermissionRequestDecision Decision { get; init; } =
            CopilotToolPermissionRequestDecision.Prompt;

        public IReadOnlyList<CopilotToolExecutionHookRun> HookRuns { get; init; } =
            Array.Empty<CopilotToolExecutionHookRun>();

        public IReadOnlyList<CopilotToolExecutionHookBinding> HookBindings { get; init; } =
            Array.Empty<CopilotToolExecutionHookBinding>();

        public bool WasCancelled { get; init; }
    }

    public sealed class CopilotWriteToolPolicyHook : ICopilotToolExecutionHook
    {
        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(CopilotToolExecutionHookContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = context.Invocation;
            var capability = invocation.Tool.Capability;
            if (capability.Access == CopilotToolAccess.ReadOnly)
                return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);

            if (invocation.AgentRequest.Mode == CopilotAgentMode.Plan)
            {
                return Task.FromResult(CopilotToolExecutionHookDecision.Deny(
                    "Plan mode permits read-only tools only.",
                    "plan_mode_write_denied"));
            }

            if (invocation.AgentRequest.Mode == CopilotAgentMode.Review)
            {
                if (invocation.Tool is not CopilotWorkspaceValidationTool
                    || !CopilotToolIntentPolicy.NeedsWorkspaceValidation(invocation.AgentRequest))
                {
                    return Task.FromResult(CopilotToolExecutionHookDecision.Deny(
                        "Review mode permits read-only tools and explicitly requested bounded workspace validation only.",
                        "review_mode_write_denied"));
                }
            }

            if (capability.RiskLevel == CopilotToolRiskLevel.High
                && capability.ApprovalMode == CopilotToolApprovalMode.Never)
            {
                return Task.FromResult(CopilotToolExecutionHookDecision.Deny(
                    "High-risk write tools must declare an approval policy.",
                    "tool_approval_policy_required"));
            }

            if (invocation.AgentRequest.Mode == CopilotAgentMode.Chat || string.IsNullOrWhiteSpace(invocation.AgentRequest.UserText))
            {
                return Task.FromResult(CopilotToolExecutionHookDecision.Deny(
                    "Write-capable tools require a non-empty explicit user request outside Chat mode.",
                    "explicit_user_request_required"));
            }

            try
            {
                if (!CopilotToolRegistry.IsAvailableForAgent(invocation.Tool, invocation.AgentRequest))
                {
                    return Task.FromResult(CopilotToolExecutionHookDecision.Deny(
                        "The tool is not available in the current Agent runtime.",
                        "tool_not_available"));
                }
            }
            catch (Exception)
            {
                return Task.FromResult(CopilotToolExecutionHookDecision.Deny(
                    "The write-tool authorization check failed.",
                    "tool_authorization_check_failed",
                    CopilotToolFailureKind.Internal));
            }

            return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(CopilotToolExecutionOutcome outcome, CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
