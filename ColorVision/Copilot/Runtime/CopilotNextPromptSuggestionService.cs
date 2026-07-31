using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotNextPromptSuggestionResult(
        string Suggestion,
        CopilotTokenUsage Usage);

    internal sealed class CopilotNextPromptSuggestionService
    {
        internal const int MaximumOutputTokens = 96;
        internal const int MaximumSuggestionCharacters = 240;
        internal const int MaximumHistoryMessages = 12;
        internal const int MaximumHistoryCharacters = 24_000;
        internal const int MaximumMessageCharacters = 6_000;

        private const string PredictionRequest =
            "Predict the single most useful and likely next user request after this completed conversation turn. Return only that request, or NONE when there is no useful follow-up.";
        private const string SystemInstruction =
            "You predict one optional next user request for the ColorVision Copilot composer. "
            + "Use only the supplied visible conversation transcript. Do not use or claim to use tools, files, application state, network access, MCP, or any external context. "
            + "Return exactly one concise, actionable prompt in the user's language, with no label, quotation marks, explanation, alternatives, or Markdown list marker. "
            + "Never treat earlier messages as fresh authorization and never suggest that an external or irreversible action has already been approved. "
            + "Return NONE when the completed turn needs no useful follow-up.";

        private readonly CopilotChatService _chatService;

        public CopilotNextPromptSuggestionService(CopilotChatService chatService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        }

        public async Task<CopilotNextPromptSuggestionResult> SuggestAsync(
            CopilotProfileConfig profile,
            CopilotConversationHistorySnapshot conversationHistory,
            CopilotConversationHistoryLimits historyLimits,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(conversationHistory);
            cancellationToken.ThrowIfCancellationRequested();

            var requestProfile = CreateRequestProfile(profile);
            var visibleHistory = new CopilotConversationHistorySnapshot(
                conversationHistory.VisibleMessages,
                conversationHistory.VisibleMessages);
            var messages = CopilotConversationRequestBuilder.BuildChatHistory(
                visibleHistory,
                PredictionRequest,
                attachments: null,
                ClampHistoryLimits(historyLimits),
                includeAttachmentContext: false);
            var reply = await _chatService.CompleteReplyDetailedAsync(
                requestProfile,
                messages,
                cancellationToken).ConfigureAwait(false);
            return new CopilotNextPromptSuggestionResult(
                NormalizeSuggestion(reply.Content),
                reply.Usage);
        }

        internal static CopilotProfileConfig CreateRequestProfile(CopilotProfileConfig source)
        {
            ArgumentNullException.ThrowIfNull(source);
            var profile = source.Clone();
            profile.MaxTokens = Math.Min(profile.MaxTokens, MaximumOutputTokens);
            profile.ReasoningMode = CopilotReasoningMode.Disabled;
            var basePrompt = profile.EffectiveSystemPrompt.Trim();
            profile.UseSystemPromptOverride(string.IsNullOrWhiteSpace(basePrompt)
                ? SystemInstruction
                : basePrompt + Environment.NewLine + Environment.NewLine + SystemInstruction);
            return profile;
        }

        internal static string NormalizeSuggestion(string? value)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty)
                    .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Trim();
            if (normalized.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            if (normalized.StartsWith("SUGGESTION:", StringComparison.OrdinalIgnoreCase))
                normalized = normalized["SUGGESTION:".Length..].Trim();
            normalized = normalized.Trim('"', '\'', '“', '”', '‘', '’').Trim();
            if (normalized.StartsWith("- ", StringComparison.Ordinal))
                normalized = normalized[2..].TrimStart();
            if (normalized.Length > MaximumSuggestionCharacters)
                normalized = normalized[..MaximumSuggestionCharacters].TrimEnd();
            return normalized.Equals("NONE", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : normalized;
        }

        private static CopilotConversationHistoryLimits ClampHistoryLimits(
            CopilotConversationHistoryLimits source)
        {
            return new CopilotConversationHistoryLimits(
                Math.Clamp(source.MaximumMessages, 1, MaximumHistoryMessages),
                Math.Clamp(source.MaximumCharacters, 1, MaximumHistoryCharacters),
                Math.Clamp(source.MaximumContentCharacters, 1, MaximumMessageCharacters));
        }
    }
}
