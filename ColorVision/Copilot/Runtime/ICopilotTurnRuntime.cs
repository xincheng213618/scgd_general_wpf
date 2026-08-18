using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal interface ICopilotTurnRuntime
    {
        IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            CancellationToken cancellationToken);

        void QueueSessionStart(
            string conversationId,
            CopilotCodexSessionStartSource source)
        {
        }

        Task<CopilotCodexSessionStartHookOutcome> RunSessionStartHooksAsync(
            CopilotAgentRequest request,
            bool hasPersistedHistory,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken) =>
            Task.FromResult(CopilotCodexSessionStartHookOutcome.Continue);

        Task<CopilotCodexSessionEndHookOutcome> RunSessionEndHooksAsync(
            CopilotAgentRequest request,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken) =>
            Task.FromResult(CopilotCodexSessionEndHookOutcome.NotRun);

        CopilotSteeringAdmissionResult EnqueueSteeringMessage(
            string taskId,
            string message);

        bool TryEnqueueBackgroundShellCommandCompletion(
            CopilotBackgroundShellCommandSnapshot snapshot);

        bool TryEnqueueBackgroundShellCommandOutput(
            CopilotBackgroundShellOutputMonitorEventArgs eventArgs);

        bool TryAnswerUserQuestion(string taskId, string requestId, string answer);

        Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken);
    }

    internal abstract record CopilotTurnEvent;

    internal enum CopilotTurnStatus
    {
        InProgress,
        Completed,
        Interrupted,
        Failed,
    }

    internal sealed record CopilotTurnError(string Code, string Message)
    {
        public const int MaximumCodeLength = 96;
        public const int MaximumMessageLength = 512;

        public bool IsStructurallyValid()
        {
            return !string.IsNullOrWhiteSpace(Code)
                && Code.Length <= MaximumCodeLength
                && !Code.Any(char.IsControl)
                && !string.IsNullOrWhiteSpace(Message)
                && Message.Length <= MaximumMessageLength
                && !Message.Any(character => char.IsControl(character)
                    && character is not '\r' and not '\n' and not '\t');
        }

        public static CopilotTurnError FromException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (exception is CopilotSessionStartHookBlockedException)
            {
                var message = CopilotApprovalRequestReason.Normalize(exception.Message);
                if (message.Length > MaximumMessageLength)
                {
                    var length = MaximumMessageLength;
                    if (char.IsHighSurrogate(message[length - 1]))
                        length--;
                    message = message[..length].TrimEnd();
                }
                return new CopilotTurnError(
                    "session_start_hook_stopped",
                    message);
            }
            if (exception is CopilotUserPromptSubmitHookBlockedException)
            {
                var message = CopilotApprovalRequestReason.Normalize(exception.Message);
                if (message.Length > MaximumMessageLength)
                {
                    var length = MaximumMessageLength;
                    if (char.IsHighSurrogate(message[length - 1]))
                        length--;
                    message = message[..length].TrimEnd();
                }
                return new CopilotTurnError(
                    "user_prompt_hook_blocked",
                    message);
            }
            return new CopilotTurnError(
                exception is TimeoutException ? "turn_timeout" : "turn_failed",
                "Copilot turn failed before producing a complete result.");
        }
    }

    internal sealed record CopilotTurnStartedEvent : CopilotTurnEvent
    {
        internal const string DefaultTurnId = "turn:local";
        internal const int MaximumTurnIdLength = 256;

        public CopilotTurnStartedEvent(CopilotAgentMode mode)
            : this(DefaultTurnId, mode)
        {
        }

        public CopilotTurnStartedEvent(string turnId, CopilotAgentMode mode)
        {
            TurnId = NormalizeTurnId(turnId);
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = mode;
        }

        public string TurnId { get; }

        public CopilotAgentMode Mode { get; }

        public CopilotTurnStatus Status { get; } = CopilotTurnStatus.InProgress;

        internal static string NormalizeTurnId(string? turnId)
        {
            var normalized = (turnId ?? string.Empty).Trim();
            if (normalized.Length == 0
                || normalized.Length > MaximumTurnIdLength
                || normalized.Any(char.IsControl))
            {
                throw new ArgumentException("A bounded non-control turn ID is required.", nameof(turnId));
            }

            return normalized;
        }
    }

    internal sealed record CopilotTurnErrorEvent : CopilotTurnEvent
    {
        public CopilotTurnErrorEvent(
            string turnId,
            CopilotAgentMode mode,
            CopilotTurnError error)
        {
            TurnId = CopilotTurnStartedEvent.NormalizeTurnId(turnId);
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (error?.IsStructurallyValid() != true)
                throw new ArgumentException("Valid bounded turn error metadata is required.", nameof(error));

            Mode = mode;
            Error = error;
        }

        public string TurnId { get; }

        public CopilotAgentMode Mode { get; }

        public CopilotTurnError Error { get; }
    }

    internal sealed record CopilotTurnRequestPreparedEvent(
        CopilotPreparedTurnRequest Request) : CopilotTurnEvent;

    internal sealed record CopilotTurnRuntimeDiagnosticEvent : CopilotTurnEvent
    {
        internal const int MaximumTextLength = 2_048;

        public CopilotTurnRuntimeDiagnosticEvent(string text)
        {
            var normalized = CopilotAgentTraceEntry.Sanitize(text);
            Text = normalized.Length <= MaximumTextLength
                ? normalized
                : normalized[..MaximumTextLength].TrimEnd();
        }

        public string Text { get; }
    }

    internal sealed record CopilotTurnChatDeltaEvent(
        CopilotStreamDelta Delta) : CopilotTurnEvent;

    internal sealed record CopilotTurnChatAnswerResetEvent : CopilotTurnEvent;

    internal sealed record CopilotTurnProviderRetryEvent(
        CopilotProviderRetryInfo Retry) : CopilotTurnEvent;

    internal sealed record CopilotTurnProviderConnectionRecoveryEvent(
        CopilotProviderConnectionRecoveryInfo Recovery) : CopilotTurnEvent;

    internal sealed record CopilotTurnReviewEnteredEvent(
        CopilotWorkspaceReviewTargetContext Target) : CopilotTurnEvent;

    internal sealed record CopilotTurnReviewExitedEvent(
        CopilotWorkspaceReviewTargetContext Target,
        string ReviewText,
        bool ReviewTextTruncated) : CopilotTurnEvent;

    internal sealed record CopilotTurnCodeReviewSnapshotUpdatedEvent(
        CopilotCodeReviewSnapshot Snapshot) : CopilotTurnEvent;

    internal sealed record CopilotTurnWorkspaceDiffUpdatedEvent(
        CopilotTurnWorkspaceDiffSnapshot Snapshot) : CopilotTurnEvent;

    internal sealed record CopilotTurnPlanUpdatedEvent(
        CopilotTurnPlanSnapshot Snapshot) : CopilotTurnEvent;

    internal sealed record CopilotTurnTokenUsageUpdatedEvent : CopilotTurnEvent
    {
        public CopilotTurnTokenUsageUpdatedEvent(CopilotTokenUsage usage)
        {
            Usage = Normalize(usage);
            if (!Usage.HasAny)
                throw new ArgumentException("A non-empty token usage snapshot is required.", nameof(usage));
        }

        public CopilotTokenUsage Usage { get; }

        internal static CopilotTokenUsage Normalize(CopilotTokenUsage usage)
        {
            var inputTokens = Math.Max(0, usage.InputTokens);
            var outputTokens = Math.Max(0, usage.OutputTokens);
            var totalTokens = usage.HasAny
                ? Math.Max(usage.EffectiveTotalTokens, AddClamped(inputTokens, outputTokens))
                : 0;
            int? cachedInputTokens = usage.HasAny && usage.CachedInputTokens.HasValue
                ? Math.Clamp(usage.CachedInputTokens.Value, 0, inputTokens)
                : null;
            return new CopilotTokenUsage(inputTokens, outputTokens, totalTokens, cachedInputTokens);
        }

        private static int AddClamped(int left, int right) =>
            (int)Math.Clamp((long)left + right, 0, int.MaxValue);
    }

    internal sealed record CopilotTurnAgentEvent(
        CopilotAgentEvent Event) : CopilotTurnEvent;

    internal sealed record CopilotTurnCompletedEvent : CopilotTurnEvent
    {
        public CopilotTurnCompletedEvent(CopilotTurnResult result)
            : this(
                CopilotTurnStartedEvent.DefaultTurnId,
                result?.Mode ?? throw new ArgumentNullException(nameof(result)),
                CopilotTurnStatus.Completed,
                result,
                error: null)
        {
        }

        private CopilotTurnCompletedEvent(
            string turnId,
            CopilotAgentMode mode,
            CopilotTurnStatus status,
            CopilotTurnResult? result,
            CopilotTurnError? error)
        {
            TurnId = CopilotTurnStartedEvent.NormalizeTurnId(turnId);
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (status == CopilotTurnStatus.InProgress || !Enum.IsDefined(status))
                throw new ArgumentOutOfRangeException(nameof(status));

            Mode = mode;
            Status = status;
            Result = result;
            Error = error;
        }

        public string TurnId { get; }

        public CopilotAgentMode Mode { get; }

        public CopilotTurnStatus Status { get; }

        public CopilotTurnResult? Result { get; }

        public CopilotTurnError? Error { get; }

        public static CopilotTurnCompletedEvent Completed(
            string turnId,
            CopilotTurnResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            return new CopilotTurnCompletedEvent(
                turnId,
                result.Mode,
                CopilotTurnStatus.Completed,
                result,
                error: null);
        }

        public static CopilotTurnCompletedEvent Interrupted(
            string turnId,
            CopilotAgentMode mode) =>
            new(
                turnId,
                mode,
                CopilotTurnStatus.Interrupted,
                result: null,
                error: null);

        public static CopilotTurnCompletedEvent Interrupted(
            string turnId,
            CopilotTurnResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            return new CopilotTurnCompletedEvent(
                turnId,
                result.Mode,
                CopilotTurnStatus.Interrupted,
                result,
                error: null);
        }

        public static CopilotTurnCompletedEvent Failed(
            string turnId,
            CopilotAgentMode mode,
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            return Failed(turnId, mode, CopilotTurnError.FromException(exception));
        }

        public static CopilotTurnCompletedEvent Failed(
            string turnId,
            CopilotAgentMode mode,
            CopilotTurnError error) =>
            new(
                turnId,
                mode,
                CopilotTurnStatus.Failed,
                result: null,
                error);
    }

    internal sealed class CopilotTurnEventSink
    {
        private readonly Action<CopilotTurnEvent> _publish;
        private readonly object _tokenUsageGate = new();
        private CopilotTokenUsage _lastTokenUsage;

        public CopilotTurnEventSink(Action<CopilotTurnEvent> publish)
        {
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        }

        public void OnRequestPrepared(CopilotPreparedTurnRequest request) =>
            _publish(new CopilotTurnRequestPreparedEvent(request));

        public void OnRuntimeDiagnostic(string text)
        {
            var diagnostic = new CopilotTurnRuntimeDiagnosticEvent(text);
            if (diagnostic.Text.Length > 0)
                _publish(diagnostic);
        }

        public void OnChatDelta(CopilotStreamDelta delta) =>
            _publish(new CopilotTurnChatDeltaEvent(delta));

        public void OnChatAnswerReset() =>
            _publish(new CopilotTurnChatAnswerResetEvent());

        public void OnProviderRetry(CopilotProviderRetryInfo retry) =>
            _publish(new CopilotTurnProviderRetryEvent(retry));

        public void OnProviderConnectionRecovery(CopilotProviderConnectionRecoveryInfo recovery) =>
            _publish(new CopilotTurnProviderConnectionRecoveryEvent(recovery));

        public void OnReviewEntered(CopilotWorkspaceReviewTargetContext target)
        {
            ArgumentNullException.ThrowIfNull(target);
            _publish(new CopilotTurnReviewEnteredEvent(target.CreateSnapshot()));
        }

        public void OnReviewExited(
            CopilotWorkspaceReviewTargetContext target,
            string reviewText,
            bool reviewTextTruncated)
        {
            ArgumentNullException.ThrowIfNull(target);
            _publish(new CopilotTurnReviewExitedEvent(
                target.CreateSnapshot(),
                reviewText ?? string.Empty,
                reviewTextTruncated));
        }

        public void OnAgentEvent(CopilotAgentEvent agentEvent) =>
            _publish(new CopilotTurnAgentEvent(agentEvent));

        public void OnCodeReviewSnapshotUpdated(CopilotCodeReviewSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!snapshot.IsStructurallyValid())
                throw new ArgumentException("Code review snapshot is invalid.", nameof(snapshot));
            _publish(new CopilotTurnCodeReviewSnapshotUpdatedEvent(snapshot.CreateSnapshot()));
        }

        public void OnWorkspaceDiffUpdated(CopilotTurnWorkspaceDiffSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            _publish(new CopilotTurnWorkspaceDiffUpdatedEvent(snapshot));
        }

        public void OnPlanUpdated(CopilotTurnPlanSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            _publish(new CopilotTurnPlanUpdatedEvent(snapshot));
        }

        public void OnTokenUsageUpdated(CopilotTokenUsage usage)
        {
            var snapshot = CopilotTurnTokenUsageUpdatedEvent.Normalize(usage);
            if (!snapshot.HasAny)
                return;

            lock (_tokenUsageGate)
            {
                if (_lastTokenUsage == snapshot)
                    return;
                _lastTokenUsage = snapshot;
                _publish(new CopilotTurnTokenUsageUpdatedEvent(snapshot));
            }
        }
    }

    internal readonly record struct CopilotPreparedTurnRequest(
        string Content,
        bool ChatAttachmentContextCaptured);

    internal sealed class CopilotTurnRequest
    {
        public CopilotTurnRequest(
            CopilotProfileConfig profile,
            CopilotAgentMode mode,
            string? userText,
            string? existingRequestContent,
            bool chatAttachmentContextCaptured,
            bool refreshExternalContext,
            CopilotAgentHostContextSnapshot hostContext,
            CopilotConversationHistoryLimits historyLimits,
            CopilotAgentSessionCheckpoint? sessionCheckpoint,
            CopilotAgentRecoveryRequest? recovery,
            CopilotAgentRunControl? runControl,
            CopilotAgentDefaultsConfig agentDefaults,
            IEnumerable<CopilotMcpClientServerConfig>? externalMcpServers,
            string? conversationId,
            string? taskId,
            CopilotAgentAccessContext? accessContext = null,
            string? activeGoalText = null,
            CopilotWorkspaceReviewTargetContext? workspaceReviewTarget = null,
            CopilotAgentSkillReference? agentSkillReference = null,
            CopilotAgentTaskEventJournalSnapshot? taskEventJournalBaseline = null)
        {
            Profile = (profile ?? throw new ArgumentNullException(nameof(profile))).Clone();
            Mode = mode;
            UserText = userText ?? string.Empty;
            ExistingRequestContent = existingRequestContent ?? string.Empty;
            ChatAttachmentContextCaptured = chatAttachmentContextCaptured;
            RefreshExternalContext = refreshExternalContext;
            HostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
            HistoryLimits = historyLimits;
            SessionCheckpoint = sessionCheckpoint;
            TaskEventJournalBaseline = taskEventJournalBaseline?.IsStructurallyValid() == true
                ? taskEventJournalBaseline
                : sessionCheckpoint?.TaskEventJournal;
            Recovery = recovery;
            RunControl = runControl;
            AgentDefaults = (agentDefaults ?? throw new ArgumentNullException(nameof(agentDefaults))).Clone();
            ConversationId = (conversationId ?? string.Empty).Trim();
            TaskId = (taskId ?? string.Empty).Trim();
            AccessContext = accessContext ?? new CopilotAgentAccessContext();
            ActiveGoalText = CopilotConversationGoal.TryNormalizeObjective(
                activeGoalText,
                out var normalizedGoal,
                out _)
                ? normalizedGoal
                : string.Empty;
            WorkspaceReviewTarget = Mode == CopilotAgentMode.Review
                && workspaceReviewTarget?.IsStructurallyValid() == true
                    ? workspaceReviewTarget.CreateSnapshot()
                    : null;
            AgentSkillReference = agentSkillReference?.IsStructurallyValid() == true
                && agentSkillReference.IsExplicitlyInvokedBy(UserText)
                    ? agentSkillReference.CreateSnapshot()
                    : null;
            ExternalMcpServers = (externalMcpServers ?? Array.Empty<CopilotMcpClientServerConfig>())
                .Where(server => server != null)
                .Select(server => server.Clone())
                .ToArray();
        }

        public CopilotProfileConfig Profile { get; }

        public CopilotAgentMode Mode { get; }

        public string UserText { get; }

        public string ExistingRequestContent { get; }

        public bool ChatAttachmentContextCaptured { get; }

        public bool RefreshExternalContext { get; }

        public CopilotAgentHostContextSnapshot HostContext { get; }

        public CopilotConversationHistoryLimits HistoryLimits { get; }

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; }

        public CopilotAgentTaskEventJournalSnapshot? TaskEventJournalBaseline { get; }

        public CopilotAgentRecoveryRequest? Recovery { get; }

        public CopilotAgentRunControl? RunControl { get; }

        public CopilotAgentDefaultsConfig AgentDefaults { get; }

        public string ConversationId { get; }

        public string TaskId { get; }

        public CopilotAgentAccessContext AccessContext { get; }

        public string ActiveGoalText { get; }

        public CopilotWorkspaceReviewTargetContext? WorkspaceReviewTarget { get; }

        public CopilotAgentSkillReference? AgentSkillReference { get; }

        public IReadOnlyList<CopilotMcpClientServerConfig> ExternalMcpServers { get; }
    }

    internal sealed class CopilotTurnResult
    {
        private CopilotTurnResult(
            CopilotAgentMode mode,
            CopilotTokenUsage usage,
            string preparedUserMessageContent,
            bool chatAttachmentContextCaptured,
            CopilotChatStreamResult? chatStreamResult,
            CopilotAgentRunResult? agentRunResult)
        {
            Mode = mode;
            Usage = usage;
            PreparedUserMessageContent = preparedUserMessageContent ?? string.Empty;
            ChatAttachmentContextCaptured = chatAttachmentContextCaptured;
            ChatStreamResult = chatStreamResult;
            AgentRunResult = agentRunResult;
        }

        public CopilotAgentMode Mode { get; }

        public CopilotTokenUsage Usage { get; }

        public string PreparedUserMessageContent { get; }

        public bool ChatAttachmentContextCaptured { get; }

        public CopilotChatStreamResult? ChatStreamResult { get; }

        public CopilotAgentRunResult? AgentRunResult { get; }

        public static CopilotTurnResult FromChat(
            CopilotTokenUsage usage,
            string preparedUserMessageContent,
            bool chatAttachmentContextCaptured,
            CopilotChatStreamResult streamResult) =>
            new(
                CopilotAgentMode.Chat,
                usage,
                preparedUserMessageContent,
                chatAttachmentContextCaptured,
                streamResult,
                agentRunResult: null);

        public static CopilotTurnResult FromAgent(
            CopilotAgentMode mode,
            CopilotTokenUsage usage,
            CopilotAgentRunResult agentRunResult) =>
            new(
                mode,
                usage,
                agentRunResult.PreparedUserMessageContent,
                chatAttachmentContextCaptured: false,
                chatStreamResult: null,
                agentRunResult: agentRunResult);
    }
}
