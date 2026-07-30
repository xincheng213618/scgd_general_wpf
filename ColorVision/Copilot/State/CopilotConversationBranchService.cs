using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotConversationBranchService
    {
        private const string BranchTitleSuffix = " · 分支";
        private const int MaximumBaseTitleLength = 48;

        public static CopilotConversationRecord CreateBranch(
            CopilotConversationRecord source,
            CopilotChatMessage throughAssistantMessage,
            string? requestedTitle = null)
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

            var forkedAtUtc = DateTimeOffset.UtcNow;
            var branch = new CopilotConversationRecord
            {
                CreatedAt = DateTime.Now,
                HasCustomTitle = true,
                IsPinned = false,
                ProfileDisplayName = source.ProfileDisplayName,
                ProfileId = source.ProfileId,
                Title = BuildBranchTitle(source.Title, requestedTitle),
                UpdatedAt = DateTime.Now,
                BranchOrigin = new CopilotConversationBranchOrigin
                {
                    ParentConversationId = source.Id,
                    RootConversationId = source.BranchOrigin?.IsStructurallyValid(source.Id) == true
                        ? source.BranchOrigin.RootConversationId
                        : source.Id,
                    ThroughMessageId = throughAssistantMessage.Id,
                    ForkedAtUtc = forkedAtUtc,
                },
                Goal = source.Goal?.IsStructurallyValid() == true
                    ? source.Goal.CopyForBranch(forkedAtUtc)
                    : null,
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

        public static CopilotChatMessage? FindLatestBranchPoint(CopilotConversationRecord source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return source.Messages.LastOrDefault(message =>
                message != null
                && !message.IsUser
                && !message.IsThinkingInProgress
                && !string.IsNullOrWhiteSpace(message.Content));
        }

        public static CopilotConversationRecord? FindBranchOriginTarget(
            IEnumerable<CopilotConversationRecord> conversations,
            CopilotConversationRecord branch)
        {
            ArgumentNullException.ThrowIfNull(conversations);
            ArgumentNullException.ThrowIfNull(branch);
            var origin = branch.BranchOrigin;
            if (origin?.IsStructurallyValid(branch.Id) != true)
                return null;

            var candidates = conversations.Where(conversation => conversation != null).ToArray();
            return candidates.FirstOrDefault(conversation => string.Equals(
                    conversation.Id,
                    origin.ParentConversationId,
                    StringComparison.Ordinal))
                ?? candidates.FirstOrDefault(conversation => string.Equals(
                    conversation.Id,
                    origin.RootConversationId,
                    StringComparison.Ordinal));
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
            foreach (var trace in clone.AgentTraceEntries)
                trace.DiscardWorkspaceRollbackAuthority();
            return clone;
        }

        private static CopilotAgentMode NormalizeRequestMode(CopilotAgentMode mode) =>
            Enum.IsDefined(mode) ? mode : CopilotAgentMode.Chat;

        private static string BuildBranchTitle(string? sourceTitle, string? requestedTitle)
        {
            var explicitTitle = requestedTitle?.Trim() ?? string.Empty;
            if (explicitTitle.Length > 0)
                return explicitTitle.Length <= CopilotConversationRecord.MaximumTitleCharacters
                    ? explicitTitle
                    : explicitTitle[..CopilotConversationRecord.MaximumTitleCharacters].TrimEnd();

            var title = string.IsNullOrWhiteSpace(sourceTitle) ? CopilotUiText.NewConversationTitle : sourceTitle.Trim();
            if (title.Length > MaximumBaseTitleLength)
                title = title[..MaximumBaseTitleLength].TrimEnd();
            return title + BranchTitleSuffix;
        }
    }
}
