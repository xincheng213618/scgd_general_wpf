using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotConversationCompactionPlan(
        CopilotRequestMessage[] SourceMessages,
        CopilotChatMessage BoundaryMessage,
        int NewSourceMessageCount,
        int NewSourceCharacters,
        int TotalSourceMessageCount,
        int TotalSourceCharacters);

    internal static class CopilotConversationCompactionPlanner
    {
        private const int MinimumNewSourceMessages = 2;
        private const int RecentMessagesToKeepVerbatim = 2;

        public static CopilotConversationCompactionPlan Create(
            CopilotConversationRecord conversation,
            CopilotConversationHistoryLimits limits,
            string compactRequest)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentException.ThrowIfNullOrWhiteSpace(compactRequest);
            if (limits.MaximumMessages <= 1 || limits.MaximumCharacters <= compactRequest.Length)
                throw new InvalidOperationException("当前模型没有足够的上下文空间来安全生成延续摘要。");

            var existingCompaction = ResolveExistingCompaction(conversation, out var startIndex);
            var pendingMessages = conversation.Messages
                .Skip(startIndex)
                .Where(message => !string.IsNullOrWhiteSpace(message.ModelContent))
                .ToArray();
            if (pendingMessages.Length < MinimumNewSourceMessages)
                throw new InvalidOperationException("至少需要一轮尚未压缩的完整对话。");

            var sourceMessages = new List<CopilotRequestMessage>();
            var remainingCharacters = limits.MaximumCharacters - compactRequest.Length;
            if (existingCompaction != null)
            {
                var summaryMessage = CopilotConversationCompactionContext.CreateSummaryMessage(existingCompaction);
                if (summaryMessage.Content.Length > remainingCharacters)
                    throw new InvalidOperationException("现有延续摘要已超过当前模型可安全处理的上下文空间。");

                sourceMessages.Add(summaryMessage);
                remainingCharacters -= summaryMessage.Content.Length;
            }

            var maximumNewMessageCount = pendingMessages.Length <= RecentMessagesToKeepVerbatim
                ? pendingMessages.Length
                : pendingMessages.Length - RecentMessagesToKeepVerbatim;
            var availableMessageSlots = limits.MaximumMessages - 1 - sourceMessages.Count;
            maximumNewMessageCount = Math.Min(maximumNewMessageCount, availableMessageSlots);

            var selectedCount = 0;
            var selectedCharacters = 0;
            var lastCompleteTurnCount = 0;
            var lastCompleteTurnCharacters = 0;
            var sourceMessageOffset = sourceMessages.Count;
            for (var index = 0; index < maximumNewMessageCount; index++)
            {
                var message = pendingMessages[index];
                var content = message.ModelContent.Trim();
                if (content.Length > remainingCharacters)
                    break;

                sourceMessages.Add(new CopilotRequestMessage(message.IsUser ? "user" : "assistant", content));
                remainingCharacters -= content.Length;
                selectedCount++;
                selectedCharacters = SaturatingAdd(selectedCharacters, content.Length);

                var nextStartsNewTurn = index + 1 >= pendingMessages.Length || pendingMessages[index + 1].IsUser;
                if (!message.IsUser && nextStartsNewTurn && selectedCount >= MinimumNewSourceMessages)
                {
                    lastCompleteTurnCount = selectedCount;
                    lastCompleteTurnCharacters = selectedCharacters;
                }
            }

            if (lastCompleteTurnCount == 0)
            {
                throw new InvalidOperationException(
                    "最早的完整对话无法在当前模型窗口内安全压缩；原有摘要和聊天记录均未改变。");
            }

            var excessMessages = selectedCount - lastCompleteTurnCount;
            if (excessMessages > 0)
                sourceMessages.RemoveRange(sourceMessageOffset + lastCompleteTurnCount, excessMessages);

            var previousMessageCount = Math.Max(0, existingCompaction?.SourceMessageCount ?? 0);
            var previousCharacters = Math.Max(0, existingCompaction?.SourceCharacters ?? 0);
            return new CopilotConversationCompactionPlan(
                sourceMessages.ToArray(),
                pendingMessages[lastCompleteTurnCount - 1],
                lastCompleteTurnCount,
                lastCompleteTurnCharacters,
                SaturatingAdd(previousMessageCount, lastCompleteTurnCount),
                SaturatingAdd(previousCharacters, lastCompleteTurnCharacters));
        }

        private static CopilotConversationCompaction? ResolveExistingCompaction(
            CopilotConversationRecord conversation,
            out int startIndex)
        {
            startIndex = 0;
            var compaction = conversation.Compaction;
            if (compaction?.IsStructurallyValid() != true)
                return null;

            for (var index = 0; index < conversation.Messages.Count; index++)
            {
                if (!string.Equals(conversation.Messages[index].Id, compaction.ThroughMessageId, StringComparison.Ordinal))
                    continue;

                startIndex = index + 1;
                return compaction;
            }

            return null;
        }

        private static int SaturatingAdd(int left, int right) =>
            (int)Math.Min(int.MaxValue, (long)Math.Max(0, left) + Math.Max(0, right));
    }
}
