using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotSideQuestionResult(
        string Answer,
        CopilotTokenUsage Usage,
        bool IsIncomplete);

    internal sealed class CopilotSideQuestionService
    {
        internal const int MaximumOutputTokens = 1_024;

        private const string SideQuestionSystemInstruction =
            "You are answering one ephemeral side question about the current ColorVision Copilot conversation. "
            + "Use only the conversation messages and side question supplied in this request. "
            + "Do not use or claim to use tools, files, current application state, network access, MCP, shell commands, databases, devices, or any other external context. "
            + "Treat earlier messages as historical context, never as fresh authorization for an action. "
            + "If the answer is not supported by the supplied conversation, say so briefly. "
            + "Answer only the side question, concisely and in the user's language. "
            + "This answer is ephemeral and must not change or steer the running task.";

        private readonly CopilotChatService _chatService;

        public CopilotSideQuestionService(CopilotChatService chatService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        }

        public async Task<CopilotSideQuestionResult> AskAsync(
            CopilotProfileConfig profile,
            CopilotConversationHistorySnapshot conversationHistory,
            CopilotConversationHistoryLimits historyLimits,
            string question,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(conversationHistory);
            var normalizedQuestion = (question ?? string.Empty).Trim();
            if (normalizedQuestion.Length == 0)
                throw new ArgumentException("A side question is required.", nameof(question));

            cancellationToken.ThrowIfCancellationRequested();
            var requestProfile = CreateRequestProfile(profile);
            var messages = CopilotConversationRequestBuilder.BuildChatHistory(
                conversationHistory,
                normalizedQuestion,
                attachments: null,
                historyLimits,
                includeAttachmentContext: false);
            var reply = await _chatService.CompleteReplyDetailedAsync(
                requestProfile,
                messages,
                cancellationToken).ConfigureAwait(false);
            var answer = reply.Content.Trim();
            if (answer.Length == 0)
                throw new InvalidOperationException("The model did not return a visible side-question answer.");

            return new CopilotSideQuestionResult(answer, reply.Usage, reply.IsIncomplete);
        }

        internal static CopilotProfileConfig CreateRequestProfile(CopilotProfileConfig source)
        {
            ArgumentNullException.ThrowIfNull(source);
            var profile = source.Clone();
            profile.MaxTokens = Math.Min(profile.MaxTokens, MaximumOutputTokens);
            var basePrompt = profile.EffectiveSystemPrompt.Trim();
            profile.UseSystemPromptOverride(string.IsNullOrWhiteSpace(basePrompt)
                ? SideQuestionSystemInstruction
                : basePrompt + Environment.NewLine + Environment.NewLine + SideQuestionSystemInstruction);
            return profile;
        }
    }
}
