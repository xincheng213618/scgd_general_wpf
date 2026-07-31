using System;
using System.Collections.Generic;
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
        internal const int MaximumSuggestionCharacters = 120;
        internal const int MaximumSuggestionWords = 16;
        internal const int MaximumHistoryMessages = 12;
        internal const int MaximumHistoryCharacters = 24_000;
        internal const int MaximumMessageCharacters = 1_500;

        private const int MinimumRepeatedSuggestionWords = 4;
        private const int MinimumRepeatedCjkCharacters = 8;

        private static readonly HashSet<string> OneWordAllowlist = new(StringComparer.OrdinalIgnoreCase)
        {
            "yes", "yeah", "yep", "no", "ok", "okay", "continue", "proceed", "push", "commit", "deploy",
            "stop", "check", "retry", "undo", "merge",
        };

        private static readonly string[] AgentVoicePrefixes =
        {
            "i'll ", "i will ", "let me ", "here's ", "here is ", "i'm going to ",
            "我来", "让我", "我会", "下面我", "接下来我",
        };

        private static readonly HashSet<string> EvaluativeFillers = new(StringComparer.OrdinalIgnoreCase)
        {
            "looks good", "looks great", "thanks", "thank you", "great", "nice", "good job",
            "不错", "很好", "看起来不错", "谢谢", "好的谢谢",
        };

        private const string PredictionRequest =
            "Predict what the user is most likely to type next after this completed conversation turn. Return only that message, or NONE when the next message is not obvious.";
        private const string SystemInstruction =
            "You predict one optional next user message for the ColorVision Copilot composer. "
            + "Use only the supplied visible conversation transcript. Do not use or claim to use tools, files, application state, network access, MCP, or any external context. "
            + "Predict what the user would actually type, not what the assistant thinks they should do. Match the user's language, style, and casing. "
            + "Return exactly one concise message of about 2-12 words, with no label, quotation marks, explanation, alternatives, Markdown, or multiple sentences. "
            + "Never repeat or rephrase a request the user already sent, use assistant-voice phrasing, ask a question back, add evaluative filler, or introduce an idea not grounded in the transcript. "
            + "Never treat earlier messages as fresh authorization and never suggest that an external or irreversible action has already been approved. "
            + "Return NONE when the next user message is not obvious, including after an error or misunderstanding.";

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
                NormalizeSuggestion(reply.Content, conversationHistory.VisibleMessages),
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

        internal static string NormalizeSuggestion(
            string? value,
            IReadOnlyList<CopilotRequestMessage>? visibleHistory = null)
        {
            var lines = (value ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length != 1)
                return string.Empty;

            var normalized = string.Join(
                " ",
                lines[0].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Trim();
            if (normalized.StartsWith("SUGGESTION:", StringComparison.OrdinalIgnoreCase))
                normalized = normalized["SUGGESTION:".Length..].Trim();
            normalized = normalized.Trim('"', '\'', '`', '“', '”', '‘', '’').Trim();

            if (IsMetaReply(normalized)
                || normalized.Length > MaximumSuggestionCharacters
                || IsMarkdown(normalized)
                || HasLabelPrefix(normalized)
                || IsAgentVoice(normalized)
                || IsEvaluativeFiller(normalized)
                || IsQuestion(normalized)
                || HasMultipleSentences(normalized)
                || HasInvalidWordCount(normalized)
                || IsRepeatOfUserMessage(normalized, visibleHistory))
            {
                return string.Empty;
            }

            return normalized;
        }

        private static bool IsMetaReply(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            string lowered = value.TrimEnd('.', '!', '?', '。', '！', '？').ToLowerInvariant();
            return lowered is "none" or "n/a" or "no suggestion" or "nothing" or "(silence)" or "silence" or "null";
        }

        private static bool IsMarkdown(string value) =>
            value.Contains('*')
            || value.Contains("```", StringComparison.Ordinal)
            || value.StartsWith('#')
            || value.StartsWith("- ", StringComparison.Ordinal)
            || value.StartsWith('[') && value.EndsWith(']')
            || value.StartsWith('(') && value.EndsWith(')');

        private static bool HasLabelPrefix(string value)
        {
            if (value.StartsWith("建议：", StringComparison.Ordinal)
                || value.StartsWith("用户：", StringComparison.Ordinal)
                || value.StartsWith("提示：", StringComparison.Ordinal))
            {
                return true;
            }

            int separator = value.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 1)
                return false;

            string head = value[..separator];
            return !head.Contains(' ') && head.All(char.IsAsciiLetter);
        }

        private static bool IsAgentVoice(string value) =>
            AgentVoicePrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        private static bool IsEvaluativeFiller(string value) =>
            EvaluativeFillers.Contains(value.TrimEnd('.', '!', '。', '！'));

        private static bool IsQuestion(string value) => value.EndsWith('?') || value.EndsWith('？');

        private static bool HasMultipleSentences(string value)
        {
            for (int index = 0; index < value.Length - 1; index++)
            {
                char current = value[index];
                if (current is '。' or '！' or '？')
                    return true;
                if (current is not ('.' or '!' or '?'))
                    continue;
                if (current == '.'
                    && index > 0
                    && char.IsDigit(value[index - 1])
                    && char.IsDigit(value[index + 1]))
                {
                    continue;
                }
                if (char.IsWhiteSpace(value[index + 1]))
                    return true;
            }
            return false;
        }

        private static bool HasInvalidWordCount(string value)
        {
            string[] words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length > MaximumSuggestionWords)
                return true;
            if (words.Length != 1 || ContainsNonAscii(value) || value.StartsWith('/'))
                return false;

            string bare = value.TrimEnd('.', '!');
            return !OneWordAllowlist.Contains(bare);
        }

        private static bool IsRepeatOfUserMessage(
            string suggestion,
            IReadOnlyList<CopilotRequestMessage>? visibleHistory)
        {
            if (visibleHistory == null || IsShortRepeatExempt(suggestion))
                return false;

            string needle = NormalizeForRepeat(suggestion);
            return visibleHistory.Any(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)
                && NormalizeForRepeat(message.Content) == needle);
        }

        private static bool IsShortRepeatExempt(string suggestion)
        {
            int words = suggestion.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
            if (!ContainsCjk(suggestion))
                return words < MinimumRepeatedSuggestionWords;

            int cjkCharacters = suggestion.Count(IsCjk);
            return cjkCharacters < MinimumRepeatedCjkCharacters;
        }

        private static string NormalizeForRepeat(string value) =>
            string.Join(
                " ",
                (value ?? string.Empty).Split(
                    [' ', '\t', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .TrimEnd('.', '!', '?', '。', '！', '？')
            .ToLowerInvariant();

        private static bool ContainsNonAscii(string value) => value.Any(character => character > 127);

        private static bool ContainsCjk(string value) => value.Any(IsCjk);

        private static bool IsCjk(char character) =>
            character is >= '\u3400' and <= '\u4DBF'
            or >= '\u4E00' and <= '\u9FFF'
            or >= '\uF900' and <= '\uFAFF';

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
