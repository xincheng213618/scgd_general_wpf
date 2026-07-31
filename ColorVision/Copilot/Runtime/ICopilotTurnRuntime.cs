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

        bool TryEnqueueSteeringMessage(string taskId, string message);

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

    internal sealed record CopilotTurnRequestPreparedEvent(
        CopilotPreparedTurnRequest Request) : CopilotTurnEvent;

    internal sealed record CopilotTurnChatDeltaEvent(
        CopilotStreamDelta Delta) : CopilotTurnEvent;

    internal sealed record CopilotTurnProviderRetryEvent(
        CopilotProviderRetryInfo Retry) : CopilotTurnEvent;

    internal sealed record CopilotTurnAgentEvent(
        CopilotAgentEvent Event) : CopilotTurnEvent;

    internal sealed record CopilotTurnCompletedEvent(
        CopilotTurnResult Result) : CopilotTurnEvent;

    internal sealed class CopilotTurnEventSink
    {
        private readonly Action<CopilotTurnEvent> _publish;

        public CopilotTurnEventSink(Action<CopilotTurnEvent> publish)
        {
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        }

        public void OnRequestPrepared(CopilotPreparedTurnRequest request) =>
            _publish(new CopilotTurnRequestPreparedEvent(request));

        public void OnChatDelta(CopilotStreamDelta delta) =>
            _publish(new CopilotTurnChatDeltaEvent(delta));

        public void OnProviderRetry(CopilotProviderRetryInfo retry) =>
            _publish(new CopilotTurnProviderRetryEvent(retry));

        public void OnAgentEvent(CopilotAgentEvent agentEvent) =>
            _publish(new CopilotTurnAgentEvent(agentEvent));
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
            string? activeGoalText = null)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Mode = mode;
            UserText = userText ?? string.Empty;
            ExistingRequestContent = existingRequestContent ?? string.Empty;
            ChatAttachmentContextCaptured = chatAttachmentContextCaptured;
            RefreshExternalContext = refreshExternalContext;
            HostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
            HistoryLimits = historyLimits;
            SessionCheckpoint = sessionCheckpoint;
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

        public CopilotAgentRecoveryRequest? Recovery { get; }

        public CopilotAgentRunControl? RunControl { get; }

        public CopilotAgentDefaultsConfig AgentDefaults { get; }

        public string ConversationId { get; }

        public string TaskId { get; }

        public CopilotAgentAccessContext AccessContext { get; }

        public string ActiveGoalText { get; }

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
