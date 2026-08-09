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
        internal const int MaximumAttachments = 32;

        public string RunId { get; set; } = string.Empty;

        public string ConversationId { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;

        public CopilotComposerStash? ComposerState { get; set; }

        public bool ShouldSerializeComposerState() => ComposerState?.HasContent == true;

        internal bool TryGetNormalized(
            out string runId,
            out string conversationId,
            out CopilotComposerStash composerState)
        {
            runId = (RunId ?? string.Empty).Trim();
            conversationId = (ConversationId ?? string.Empty).Trim();
            var prompt = string.IsNullOrWhiteSpace(ComposerState?.Text)
                ? (Prompt ?? string.Empty).Trim()
                : ComposerState.Text.Trim();
            var attachments = ComposerState?.Attachments
                ?? new ObservableCollection<CopilotAttachmentItem>();
            composerState = CopilotComposerStash.Capture(
                prompt,
                prompt.Length,
                Enum.IsDefined(ComposerState?.RequestMode ?? CopilotAgentMode.Auto)
                    ? ComposerState?.RequestMode ?? CopilotAgentMode.Auto
                    : CopilotAgentMode.Auto,
                attachments,
                ComposerState?.WorkspaceReviewTarget,
                ComposerState?.AgentSkillReference);
            return runId.Length is > 0 and <= MaximumIdentifierCharacters
                && conversationId.Length is > 0 and <= MaximumIdentifierCharacters
                && prompt.Length is > 0 and <= MaximumPromptCharacters
                && attachments.Count <= MaximumAttachments
                && attachments.All(attachment => attachment != null);
        }

        internal IEnumerable<CopilotAttachmentItem> EnumerateReferencedAttachments() =>
            ComposerState?.Attachments?.Where(attachment => attachment != null)
            ?? Enumerable.Empty<CopilotAttachmentItem>();
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
            var recoveriesByConversation = new Dictionary<string, List<CopilotComposerStash>>(StringComparer.Ordinal);
            var seenRunIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var record in state.QueuedFollowUpRecoveries.Take(CopilotAgentTaskHost.MaximumQueuedRuns))
            {
                if (record == null
                    || !record.TryGetNormalized(out var runId, out var conversationId, out var composerState)
                    || !seenRunIds.Add(runId)
                    || !conversationsById.ContainsKey(conversationId))
                {
                    continue;
                }

                if (!recoveriesByConversation.TryGetValue(conversationId, out var recoveries))
                {
                    recoveries = [];
                    recoveriesByConversation.Add(conversationId, recoveries);
                }
                recoveries.Add(composerState);
            }

            foreach (var pair in recoveriesByConversation)
            {
                var conversation = conversationsById[pair.Key];
                var recoveredModes = pair.Value
                    .Select(recovery => recovery.RequestMode)
                    .Distinct()
                    .ToArray();
                var hasExistingModeConflict = conversation.DraftRequestMode != CopilotAgentMode.Auto
                    && recoveredModes.Any(mode => mode != conversation.DraftRequestMode);
                var prompts = recoveredModes.Length <= 1 && !hasExistingModeConflict
                    ? pair.Value.Select(recovery => recovery.Text).ToArray()
                    : pair.Value.Select(recovery =>
                        $"[{FormatMode(recovery.RequestMode)}] {recovery.Text}").ToArray();
                var restoredDraft = FormatRestoredDraft(prompts);
                if (string.IsNullOrWhiteSpace(restoredDraft))
                    continue;

                var existingDraft = (conversation.DraftText ?? string.Empty).TrimEnd();
                if (!string.Equals(existingDraft.Trim(), restoredDraft.Trim(), StringComparison.Ordinal))
                {
                    conversation.DraftText = string.IsNullOrWhiteSpace(existingDraft)
                        ? restoredDraft
                        : existingDraft + Environment.NewLine + Environment.NewLine + restoredDraft;
                }
                var recoveredSkillReference = pair.Value.Count == 1
                    ? pair.Value[0].AgentSkillReference
                    : null;
                if (string.IsNullOrWhiteSpace(existingDraft)
                    && pair.Value.Count == 1
                    && recoveredSkillReference?.IsExplicitlyInvokedBy(conversation.DraftText) == true)
                {
                    conversation.DraftAgentSkillReference = recoveredSkillReference.CreateSnapshot();
                }
                RestoreAttachments(conversation, pair.Value);
                if (conversation.DraftRequestMode == CopilotAgentMode.Auto
                    && recoveredModes.Length == 1)
                {
                    conversation.DraftRequestMode = recoveredModes[0];
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

        private static void RestoreAttachments(
            CopilotConversationRecord conversation,
            IEnumerable<CopilotComposerStash> recoveries)
        {
            var identities = conversation.Attachments
                .Where(attachment => attachment != null)
                .Select(BuildAttachmentIdentity)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var attachment in recoveries
                .SelectMany(recovery => recovery.CreateAttachmentSnapshots()))
            {
                if (identities.Add(BuildAttachmentIdentity(attachment)))
                    conversation.Attachments.Add(attachment);
            }
        }

        private static string BuildAttachmentIdentity(CopilotAttachmentItem attachment)
        {
            if (!string.IsNullOrWhiteSpace(attachment.Id))
                return "id:" + attachment.Id.Trim();

            return string.Join(
                "\0",
                (int)attachment.Type,
                attachment.Title,
                attachment.Value,
                attachment.Source);
        }

        private static string FormatMode(CopilotAgentMode mode) => mode switch
        {
            CopilotAgentMode.Chat => "Chat",
            CopilotAgentMode.Explain => "Explain",
            CopilotAgentMode.Web => "Web",
            CopilotAgentMode.Code => "Code",
            CopilotAgentMode.Review => "Review",
            CopilotAgentMode.Diagnose => "Diagnose",
            CopilotAgentMode.Plan => "Plan",
            _ => "Auto",
        };
    }
}
