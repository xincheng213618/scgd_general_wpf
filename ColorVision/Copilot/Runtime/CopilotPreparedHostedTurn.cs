using System;

namespace ColorVision.Copilot
{
    internal sealed class CopilotPreparedHostedTurn
    {
        public CopilotPreparedHostedTurn(
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotAgentHostContextSnapshot hostContext,
            CopilotTurnRuntimeConfigSnapshot runtimeConfig,
            bool refreshExternalContext,
            bool isAutomaticGoalContinuation)
        {
            Conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
            RequestProfile = requestProfile ?? throw new ArgumentNullException(nameof(requestProfile));
            UserMessage = userMessage ?? throw new ArgumentNullException(nameof(userMessage));
            AssistantMessage = assistantMessage ?? throw new ArgumentNullException(nameof(assistantMessage));
            HostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
            RuntimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
            if (!UserMessage.IsUser)
                throw new ArgumentException("The prepared user message must have the user role.", nameof(userMessage));
            if (AssistantMessage.IsUser)
                throw new ArgumentException("The prepared assistant message must have the assistant role.", nameof(assistantMessage));

            RefreshExternalContext = refreshExternalContext;
            IsAutomaticGoalContinuation = isAutomaticGoalContinuation;
        }

        public CopilotConversationRecord Conversation { get; }

        public CopilotProfileConfig RequestProfile { get; }

        public CopilotChatMessage UserMessage { get; }

        public CopilotChatMessage AssistantMessage { get; }

        public CopilotAgentHostContextSnapshot HostContext { get; }

        public CopilotTurnRuntimeConfigSnapshot RuntimeConfig { get; }

        public bool RefreshExternalContext { get; }

        public bool IsAutomaticGoalContinuation { get; }

        public string ConversationId => Conversation.Id;

        public CopilotAgentMode Mode => UserMessage.RequestMode;

        public void ValidateHostedRun(CopilotHostedAgentRun hostedRun)
        {
            ArgumentNullException.ThrowIfNull(hostedRun);
            if (!string.Equals(hostedRun.ConversationId, ConversationId, StringComparison.Ordinal)
                || hostedRun.Mode != Mode)
            {
                throw new InvalidOperationException("The hosted run does not match the prepared Copilot turn.");
            }
        }
    }
}
