using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotQueuedFollowUpRecoveryRecord
    {
        internal const int MaximumIdentifierCharacters = 128;
        internal const int MaximumPromptCharacters = CopilotConversationHistoryWindow.MaximumContentCharacterLimit;

        public string RunId { get; set; } = string.Empty;

        public string ConversationId { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;

        internal bool TryGetNormalized(out string runId, out string conversationId, out string prompt)
        {
            runId = (RunId ?? string.Empty).Trim();
            conversationId = (ConversationId ?? string.Empty).Trim();
            prompt = (Prompt ?? string.Empty).Trim();
            return runId.Length is > 0 and <= MaximumIdentifierCharacters
                && conversationId.Length is > 0 and <= MaximumIdentifierCharacters
                && prompt.Length is > 0 and <= MaximumPromptCharacters;
        }
    }

    internal static class CopilotQueuedFollowUpRecovery
    {
        internal static bool RestoreToDrafts(CopilotChatState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            state.RecoveredQueuedFollowUpCount = 0;
            if (state.QueuedFollowUpRecoveries == null)
            {
                state.QueuedFollowUpRecoveries = new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>();
                return true;
            }
            if (state.QueuedFollowUpRecoveries.Count == 0)
                return false;

            var conversationsById = (state.Conversations ?? new ObservableCollection<CopilotConversationRecord>())
                .Where(conversation => conversation != null && !string.IsNullOrWhiteSpace(conversation.Id))
                .GroupBy(conversation => conversation.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var promptsByConversation = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var seenRunIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var record in state.QueuedFollowUpRecoveries.Take(CopilotAgentTaskHost.MaximumQueuedRuns))
            {
                if (record == null
                    || !record.TryGetNormalized(out var runId, out var conversationId, out var prompt)
                    || !seenRunIds.Add(runId)
                    || !conversationsById.ContainsKey(conversationId))
                {
                    continue;
                }

                if (!promptsByConversation.TryGetValue(conversationId, out var prompts))
                {
                    prompts = [];
                    promptsByConversation.Add(conversationId, prompts);
                }
                prompts.Add(prompt);
            }

            foreach (var pair in promptsByConversation)
            {
                var conversation = conversationsById[pair.Key];
                var restoredDraft = FormatRestoredDraft(pair.Value);
                if (string.IsNullOrWhiteSpace(restoredDraft))
                    continue;

                var existingDraft = (conversation.DraftText ?? string.Empty).TrimEnd();
                if (!string.Equals(existingDraft.Trim(), restoredDraft.Trim(), StringComparison.Ordinal))
                {
                    conversation.DraftText = string.IsNullOrWhiteSpace(existingDraft)
                        ? restoredDraft
                        : existingDraft + Environment.NewLine + Environment.NewLine + restoredDraft;
                }
                state.RecoveredQueuedFollowUpCount += pair.Value.Count;
            }

            state.QueuedFollowUpRecoveries.Clear();
            return true;
        }

        internal static string FormatRestoredDraft(IReadOnlyList<string> prompts)
        {
            ArgumentNullException.ThrowIfNull(prompts);
            if (prompts.Count == 0)
                return string.Empty;
            if (prompts.Count == 1)
                return prompts[0];

            return "以下排队后续尚未执行，请检查后重新发送：" + Environment.NewLine + Environment.NewLine
                + string.Join(
                    Environment.NewLine + Environment.NewLine,
                    prompts.Select((prompt, index) => $"{index + 1}. {prompt}"));
        }
    }
}
