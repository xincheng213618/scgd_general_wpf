using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal interface ICopilotTurnRuntime
    {
        Task<CopilotTurnResult> RunAsync(
            CopilotTurnRequest request,
            ICopilotTurnEventSink eventSink,
            CancellationToken cancellationToken);

        bool TryEnqueueSteeringMessage(string message);
    }

    internal interface ICopilotTurnEventSink
    {
        void OnRequestPrepared(CopilotPreparedTurnRequest request);

        void OnChatDelta(CopilotStreamDelta delta);

        void OnProviderRetry(CopilotProviderRetryInfo retry);

        void OnAgentEvent(CopilotAgentEvent agentEvent);
    }

    internal sealed class CopilotTurnEventSink : ICopilotTurnEventSink
    {
        private readonly Action<CopilotPreparedTurnRequest> _onRequestPrepared;
        private readonly Action<CopilotStreamDelta> _onChatDelta;
        private readonly Action<CopilotProviderRetryInfo> _onProviderRetry;
        private readonly Action<CopilotAgentEvent> _onAgentEvent;

        public CopilotTurnEventSink(
            Action<CopilotPreparedTurnRequest> onRequestPrepared,
            Action<CopilotStreamDelta> onChatDelta,
            Action<CopilotProviderRetryInfo> onProviderRetry,
            Action<CopilotAgentEvent> onAgentEvent)
        {
            _onRequestPrepared = onRequestPrepared ?? throw new ArgumentNullException(nameof(onRequestPrepared));
            _onChatDelta = onChatDelta ?? throw new ArgumentNullException(nameof(onChatDelta));
            _onProviderRetry = onProviderRetry ?? throw new ArgumentNullException(nameof(onProviderRetry));
            _onAgentEvent = onAgentEvent ?? throw new ArgumentNullException(nameof(onAgentEvent));
        }

        public void OnRequestPrepared(CopilotPreparedTurnRequest request) => _onRequestPrepared(request);

        public void OnChatDelta(CopilotStreamDelta delta) => _onChatDelta(delta);

        public void OnProviderRetry(CopilotProviderRetryInfo retry) => _onProviderRetry(retry);

        public void OnAgentEvent(CopilotAgentEvent agentEvent) => _onAgentEvent(agentEvent);
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
            IEnumerable<CopilotMcpClientServerConfig>? externalMcpServers)
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
