using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal static class CopilotConversationBranchService
    {
        private const string BranchTitleSuffix = " · 分支";
        private const int MaximumBaseTitleLength = 48;

        public static CopilotConversationRecord CreateBranch(
            CopilotConversationRecord source,
            CopilotChatMessage throughAssistantMessage)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(throughAssistantMessage);
            var throughIndex = source.Messages.IndexOf(throughAssistantMessage);
            if (throughIndex < 0
                || throughAssistantMessage.IsUser
                || throughAssistantMessage.IsThinkingInProgress)
            {
                throw new InvalidOperationException("A branch requires a completed assistant message from the source conversation.");
            }

            var branch = new CopilotConversationRecord
            {
                CreatedAt = DateTime.Now,
                HasCustomTitle = true,
                IsPinned = false,
                ProfileDisplayName = source.ProfileDisplayName,
                ProfileId = source.ProfileId,
                Title = BuildBranchTitle(source.Title),
                UpdatedAt = DateTime.Now,
            };
            var messageIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var lastUserMode = CopilotAgentMode.Chat;
            for (var index = 0; index <= throughIndex; index++)
            {
                var sourceMessage = source.Messages[index];
                if (sourceMessage.IsUser)
                    lastUserMode = NormalizeRequestMode(sourceMessage.RequestMode);

                var clonedMessage = CloneMessage(sourceMessage, lastUserMode);
                branch.Messages.Add(clonedMessage);
                if (!string.IsNullOrWhiteSpace(sourceMessage.Id))
                    messageIdMap.TryAdd(sourceMessage.Id, clonedMessage.Id);
            }

            if (source.Compaction?.IsStructurallyValid() == true
                && messageIdMap.TryGetValue(source.Compaction.ThroughMessageId, out var branchBoundaryId))
            {
                branch.Compaction = new CopilotConversationCompaction
                {
                    CreatedAtUtc = source.Compaction.CreatedAtUtc,
                    SourceCharacters = source.Compaction.SourceCharacters,
                    SourceMessageCount = source.Compaction.SourceMessageCount,
                    StrategyVersion = source.Compaction.StrategyVersion,
                    Summary = source.Compaction.Summary,
                    ThroughMessageId = branchBoundaryId,
                };
            }

            branch.RefreshSummary();
            return branch;
        }

        private static CopilotChatMessage CloneMessage(CopilotChatMessage source, CopilotAgentMode lastUserMode)
        {
            var serializedMessage = JsonConvert.SerializeObject(source, Formatting.None);
            var clone = JsonConvert.DeserializeObject<CopilotChatMessage>(serializedMessage)
                ?? throw new InvalidOperationException("The source message could not be copied into the conversation branch.");

            clone.Id = Guid.NewGuid().ToString("N");
            clone.RequestMode = source.IsUser ? NormalizeRequestMode(source.RequestMode) : lastUserMode;
            clone.RecoveryRequest = null;
            foreach (var attachment in clone.Attachments)
                attachment.Id = Guid.NewGuid().ToString("N");
            clone.EnsureValid();
            return clone;
        }

        private static CopilotAgentMode NormalizeRequestMode(CopilotAgentMode mode) =>
            Enum.IsDefined(mode) ? mode : CopilotAgentMode.Chat;

        private static string BuildBranchTitle(string? sourceTitle)
        {
            var title = string.IsNullOrWhiteSpace(sourceTitle) ? CopilotUiText.NewConversationTitle : sourceTitle.Trim();
            if (title.Length > MaximumBaseTitleLength)
                title = title[..MaximumBaseTitleLength].TrimEnd();
            return title + BranchTitleSuffix;
        }
    }
}
