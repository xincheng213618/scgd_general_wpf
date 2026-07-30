using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotConversationBranchService
    {
        private const string BranchTitleSuffix = " · 分支";
        private const string ForkSnapshotInterruptionDetail =
            "此回答是在源会话仍运行时创建的分支快照；已保留当时可见内容，但未完成的模型、工具与授权状态不会在分支中继续。源会话仍会继续运行。";
        private const string ForkSnapshotMarker =
            "[会话分支快照：上一个回答在源会话中仍在生成；这里只保留创建分支时可见的内容，未完成的工具执行不会在此分支继续。]";
        private const int MaximumBaseTitleLength = 48;

        public static CopilotConversationRecord CreateBranch(
            CopilotConversationRecord source,
            CopilotChatMessage throughAssistantMessage,
            string? requestedTitle = null)
        {
            return CreateBranchCore(
                source,
                throughAssistantMessage,
                requestedTitle,
                allowInProgressSnapshot: false);
        }

        public static CopilotConversationRecord CreateCurrentBranch(
            CopilotConversationRecord source,
            string? requestedTitle = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            var branchPoint = FindCurrentBranchPoint(source)
                ?? throw new InvalidOperationException("A branch requires an assistant message from the source conversation.");
            return CreateBranchCore(
                source,
                branchPoint,
                requestedTitle,
                allowInProgressSnapshot: true);
        }

        private static CopilotConversationRecord CreateBranchCore(
            CopilotConversationRecord source,
            CopilotChatMessage throughAssistantMessage,
            string? requestedTitle,
            bool allowInProgressSnapshot)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(throughAssistantMessage);
            var throughIndex = source.Messages.IndexOf(throughAssistantMessage);
            if (throughIndex < 0
                || throughAssistantMessage.IsUser
                || (throughAssistantMessage.IsThinkingInProgress && !allowInProgressSnapshot))
            {
                throw new InvalidOperationException("A branch requires a completed assistant message from the source conversation.");
            }

            var capturesInProgressTurn = throughAssistantMessage.IsThinkingInProgress;
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

                var clonedMessage = CloneMessage(
                    sourceMessage,
                    lastUserMode,
                    capturesInProgressTurn && ReferenceEquals(sourceMessage, throughAssistantMessage));
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

        public static CopilotChatMessage? FindCurrentBranchPoint(CopilotConversationRecord source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return source.Messages.LastOrDefault(message =>
                message != null
                && !message.IsUser
                && (message.IsThinkingInProgress || !string.IsNullOrWhiteSpace(message.Content)));
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

        private static CopilotChatMessage CloneMessage(
            CopilotChatMessage source,
            CopilotAgentMode lastUserMode,
            bool captureInProgressSnapshot)
        {
            var serializedMessage = JsonConvert.SerializeObject(source, Formatting.None);
            var clone = JsonConvert.DeserializeObject<CopilotChatMessage>(serializedMessage)
                ?? throw new InvalidOperationException("The source message could not be copied into the conversation branch.");

            clone.Id = Guid.NewGuid().ToString("N");
            clone.RequestMode = source.IsUser ? NormalizeRequestMode(source.RequestMode) : lastUserMode;
            clone.RecoveryRequest = null;
            foreach (var attachment in clone.Attachments)
                attachment.Id = Guid.NewGuid().ToString("N");
            if (captureInProgressSnapshot)
                CompleteForkSnapshot(clone);
            clone.EnsureValid();
            foreach (var trace in clone.AgentTraceEntries)
                trace.DiscardWorkspaceRollbackAuthority();
            return clone;
        }

        private static void CompleteForkSnapshot(CopilotChatMessage assistantMessage)
        {
            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;
            assistantMessage.CompleteActiveAgentTraces(
                CopilotToolExecutionState.Interrupted,
                CopilotToolFailureKind.Internal,
                "fork_snapshot_incomplete",
                "Tool execution was still in progress when the conversation branch snapshot was captured and is not running in this branch.");
            assistantMessage.MarkThinkingCompleted();
            assistantMessage.IsContentDisplayOnly = false;
            assistantMessage.RequestContent = string.Empty;
            var marker = string.IsNullOrWhiteSpace(assistantMessage.Content)
                ? ForkSnapshotMarker
                : Environment.NewLine + Environment.NewLine + ForkSnapshotMarker;
            if (assistantMessage.UsesResponseTimeline)
                assistantMessage.AppendResponseTimelineText(marker);
            else
                assistantMessage.Content = (assistantMessage.Content ?? string.Empty).TrimEnd() + marker;
            assistantMessage.MarkResponseInterrupted(ForkSnapshotInterruptionDetail);
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
