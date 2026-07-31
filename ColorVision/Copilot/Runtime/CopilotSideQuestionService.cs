using System;
using System.Collections.Generic;
using System.Linq;
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
            "You are answering an ephemeral side conversation forked from the current ColorVision Copilot conversation. "
            + "Use only the frozen parent conversation messages, earlier side-conversation turns, and current side question supplied in this request. "
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
            IReadOnlyList<CopilotRequestMessage>? sideTranscript,
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
            var requestHistory = MergeConversationHistory(conversationHistory, sideTranscript);
            var messages = CopilotConversationRequestBuilder.BuildChatHistory(
                requestHistory,
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

        internal static CopilotConversationHistorySnapshot MergeConversationHistory(
            CopilotConversationHistorySnapshot conversationHistory,
            IReadOnlyList<CopilotRequestMessage>? sideTranscript)
        {
            ArgumentNullException.ThrowIfNull(conversationHistory);
            var normalizedTranscript = (sideTranscript ?? Array.Empty<CopilotRequestMessage>())
                .Where(message => (string.Equals(message.Role, "user", StringComparison.Ordinal)
                        || string.Equals(message.Role, "assistant", StringComparison.Ordinal))
                    && !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new CopilotRequestMessage(message.Role, message.Content.Trim()))
                .ToArray();
            if (normalizedTranscript.Length == 0)
                return conversationHistory;

            return new CopilotConversationHistorySnapshot(
                conversationHistory.ModelMessages.Concat(normalizedTranscript),
                conversationHistory.VisibleMessages);
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
