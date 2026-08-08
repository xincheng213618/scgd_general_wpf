using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotConversationBranchFamilyMember
    {
        public CopilotConversationBranchFamilyMember(
            CopilotConversationRecord conversation,
            int depth,
            bool isRoot,
            bool isCurrent,
            bool hasMissingParent)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            Conversation = conversation;
            Depth = Math.Max(0, depth);
            IsRoot = isRoot;
            IsCurrent = isCurrent;
            HasMissingParent = hasMissingParent;
        }

        public CopilotConversationRecord Conversation { get; }

        public int Depth { get; }

        public bool IsRoot { get; }

        public bool IsCurrent { get; }

        public bool HasMissingParent { get; }

        public string DisplayLabel
        {
            get
            {
                var title = string.IsNullOrWhiteSpace(Conversation.Title)
                    ? CopilotUiText.NewConversationTitle
                    : Conversation.Title.Trim();
                if (IsRoot)
                    return $"根会话 · {title}";

                var indent = new string(' ', Math.Min(Depth, 6) * 2);
                var missingParent = HasMissingParent ? "源会话缺失 · " : string.Empty;
                return $"{indent}↳ {missingParent}{title}";
            }
        }
    }

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

        public static CopilotConversationRecord CreateRewindBranch(
            CopilotConversationRecord source,
            CopilotChatMessage fromUserMessage,
            string? requestedTitle = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(fromUserMessage);
            var userIndex = source.Messages.IndexOf(fromUserMessage);
            if (userIndex < 0
                || !fromUserMessage.IsUser
                || string.IsNullOrWhiteSpace(fromUserMessage.Id)
                || string.IsNullOrWhiteSpace(fromUserMessage.Content))
            {
                throw new InvalidOperationException("A conversation rewind requires a visible user message from the source conversation.");
            }

            var copyThroughIndex = FindPreviousCompletedAssistantIndex(source, userIndex - 1);
            return CreateCopiedBranch(
                source,
                fromUserMessage.Id,
                copyThroughIndex,
                requestedTitle,
                capturesInProgressTurn: false);
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
            return CreateCopiedBranch(
                source,
                throughAssistantMessage.Id,
                throughIndex,
                requestedTitle,
                capturesInProgressTurn);
        }

        private static CopilotConversationRecord CreateCopiedBranch(
            CopilotConversationRecord source,
            string originThroughMessageId,
            int copyThroughIndex,
            string? requestedTitle,
            bool capturesInProgressTurn)
        {
            var forkedAtUtc = DateTimeOffset.UtcNow;
            var forkedAt = forkedAtUtc.LocalDateTime;
            var copiedGoal = source.Goal?.IsStructurallyValid() == true
                ? source.Goal.CopyForBranch(forkedAtUtc)
                : null;
            var branch = new CopilotConversationRecord
            {
                CreatedAt = forkedAt,
                HasCustomTitle = true,
                IsPinned = false,
                ProfileDisplayName = source.ProfileDisplayName,
                ProfileId = source.ProfileId,
                ResponsePersonality = source.ResponsePersonality,
                AdditionalReadRootPaths = new ObservableCollection<string>(
                    CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(source.AdditionalReadRootPaths)),
                Title = BuildBranchTitle(source.Title, requestedTitle),
                UpdatedAt = forkedAt,
                RecencyAt = forkedAt,
                BranchOrigin = new CopilotConversationBranchOrigin
                {
                    ParentConversationId = source.Id,
                    RootConversationId = source.BranchOrigin?.IsStructurallyValid(source.Id) == true
                        ? source.BranchOrigin.RootConversationId
                        : source.Id,
                    ThroughMessageId = originThroughMessageId,
                    ForkedAtUtc = forkedAtUtc,
                },
                Goal = copiedGoal,
                IsGoalContinuationDeferred = copiedGoal?.IsActive == true,
            };
            var messageIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var lastUserMode = CopilotAgentMode.Chat;
            for (var index = 0; index <= copyThroughIndex; index++)
            {
                var sourceMessage = source.Messages[index];
                if (sourceMessage.IsUser)
                    lastUserMode = NormalizeRequestMode(sourceMessage.RequestMode);

                var clonedMessage = CloneMessage(
                    sourceMessage,
                    lastUserMode,
                    capturesInProgressTurn && index == copyThroughIndex);
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
                branch.CompactionUsage = source.CompactionUsage?.Copy();
            }
            if (copyThroughIndex >= 0)
                branch.TitleGenerationUsage = source.TitleGenerationUsage?.Copy();

            branch.RefreshSummary();
            return branch;
        }

        private static int FindPreviousCompletedAssistantIndex(
            CopilotConversationRecord source,
            int startIndex)
        {
            for (var index = Math.Min(startIndex, source.Messages.Count - 1); index >= 0; index--)
            {
                var message = source.Messages[index];
                if (!message.IsUser
                    && !message.IsThinkingInProgress
                    && !string.IsNullOrWhiteSpace(message.Content))
                {
                    return index;
                }
            }

            return -1;
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

        public static IReadOnlyList<CopilotConversationBranchFamilyMember> BuildBranchFamily(
            IEnumerable<CopilotConversationRecord> conversations,
            CopilotConversationRecord? selectedConversation)
        {
            ArgumentNullException.ThrowIfNull(conversations);
            if (selectedConversation == null || string.IsNullOrWhiteSpace(selectedConversation.Id))
                return Array.Empty<CopilotConversationBranchFamilyMember>();

            var conversationsById = new Dictionary<string, CopilotConversationRecord>(StringComparer.Ordinal);
            foreach (var conversation in conversations.Where(conversation => conversation != null))
            {
                if (!string.IsNullOrWhiteSpace(conversation.Id))
                    conversationsById.TryAdd(conversation.Id, conversation);
            }
            conversationsById[selectedConversation.Id] = selectedConversation;

            var selectedOrigin = selectedConversation.BranchOrigin;
            var rootConversationId = selectedOrigin?.IsStructurallyValid(selectedConversation.Id) == true
                ? selectedOrigin.RootConversationId
                : selectedConversation.Id;
            var family = conversationsById.Values
                .Where(conversation =>
                    string.Equals(conversation.Id, rootConversationId, StringComparison.Ordinal)
                    || (conversation.BranchOrigin?.IsStructurallyValid(conversation.Id) == true
                        && string.Equals(
                            conversation.BranchOrigin.RootConversationId,
                            rootConversationId,
                            StringComparison.Ordinal)))
                .ToDictionary(conversation => conversation.Id, StringComparer.Ordinal);
            if (!family.ContainsKey(selectedConversation.Id))
                family[selectedConversation.Id] = selectedConversation;

            var orderedChildren = family.Values
                .Where(conversation => conversation.BranchOrigin?.IsStructurallyValid(conversation.Id) == true)
                .GroupBy(conversation => conversation.BranchOrigin!.ParentConversationId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(GetBranchSortTime)
                        .ThenBy(conversation => conversation.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(conversation => conversation.Id, StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal);
            var orderedFamily = family.Values
                .OrderBy(GetBranchSortTime)
                .ThenBy(conversation => conversation.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(conversation => conversation.Id, StringComparer.Ordinal)
                .ToArray();
            var familyIds = family.Keys.ToHashSet(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<CopilotConversationBranchFamilyMember>(family.Count);

            void AppendSubtree(CopilotConversationRecord start, int initialDepth)
            {
                var pending = new Stack<(CopilotConversationRecord Conversation, int Depth)>();
                pending.Push((start, initialDepth));
                while (pending.Count > 0)
                {
                    var (conversation, depth) = pending.Pop();
                    if (!visited.Add(conversation.Id))
                        continue;

                    var origin = conversation.BranchOrigin;
                    var isRoot = string.Equals(conversation.Id, rootConversationId, StringComparison.Ordinal);
                    var hasMissingParent = !isRoot
                        && origin?.IsStructurallyValid(conversation.Id) == true
                        && !familyIds.Contains(origin.ParentConversationId);
                    result.Add(new CopilotConversationBranchFamilyMember(
                        conversation,
                        Math.Min(depth, 12),
                        isRoot,
                        string.Equals(conversation.Id, selectedConversation.Id, StringComparison.Ordinal),
                        hasMissingParent));

                    if (!orderedChildren.TryGetValue(conversation.Id, out var children))
                        continue;

                    for (var index = children.Length - 1; index >= 0; index--)
                        pending.Push((children[index], depth + 1));
                }
            }

            if (family.TryGetValue(rootConversationId, out var root))
                AppendSubtree(root, 0);

            foreach (var orphan in orderedFamily.Where(conversation =>
                         conversation.BranchOrigin?.IsStructurallyValid(conversation.Id) == true
                         && !familyIds.Contains(conversation.BranchOrigin.ParentConversationId)))
            {
                if (!visited.Contains(orphan.Id))
                    AppendSubtree(orphan, 1);
            }

            foreach (var conversation in orderedFamily)
            {
                if (!visited.Contains(conversation.Id))
                    AppendSubtree(conversation, 1);
            }

            return result;
        }

        private static DateTimeOffset GetBranchSortTime(CopilotConversationRecord conversation)
        {
            if (conversation.BranchOrigin?.IsStructurallyValid(conversation.Id) == true)
                return conversation.BranchOrigin.ForkedAtUtc;

            return conversation.CreatedAt == default
                ? DateTimeOffset.MinValue
                : new DateTimeOffset(conversation.CreatedAt);
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
