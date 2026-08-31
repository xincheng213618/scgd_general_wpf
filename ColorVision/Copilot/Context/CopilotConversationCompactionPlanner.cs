using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed record CopilotConversationCompactionPlan(
        CopilotRequestMessage[] SourceMessages,
        CopilotChatMessage BoundaryMessage,
        CopilotConversationCompactionTerminalEvidence TerminalEvidence,
        int NewSourceMessageCount,
        int NewSourceCharacters,
        int TotalSourceMessageCount,
        int TotalSourceCharacters,
        long SourceEstimatedWeight,
        string SourceRevision)
    {
        public void EnsureSummaryShrinks(string summary)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(summary);
            var summaryWeight = CopilotConversationCompactionContext.EstimateSummaryWeight(summary);
            if (summaryWeight >= SourceEstimatedWeight)
            {
                throw new InvalidOperationException(
                    $"模型返回的摘要没有缩小上下文（摘要估算 {summaryWeight:N0}，被替换内容估算 {SourceEstimatedWeight:N0}）；原有摘要和聊天记录均未改变。请缩小聚焦范围后重试。");
            }
        }

        public void EnsureSourceStillCurrent(
            CopilotConversationRecord conversation)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            var currentRevision =
                CopilotConversationCompactionPlanner.CreateSourceRevision(
                    conversation,
                    BoundaryMessage.Id);
            if (currentRevision.Length == 0
                || !string.Equals(
                    SourceRevision,
                    currentRevision,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "压缩期间源对话或已有摘要已发生变化，旧摘要未应用；请重试 /compact。");
            }
        }
    }

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
            var compactRequestWeight = CopilotTokenEstimator.EstimateTextWeight(compactRequest);
            if (limits.MaximumMessages <= 1 || limits.MaximumCharacters <= compactRequestWeight)
                throw new InvalidOperationException("当前模型没有足够的上下文空间来安全生成延续摘要。");

            var existingCompaction = ResolveExistingCompaction(conversation, out var startIndex);
            var pendingMessages = conversation.Messages
                .Skip(startIndex)
                .Where(message => !string.IsNullOrWhiteSpace(message.ModelContent))
                .ToArray();
            if (pendingMessages.Length < MinimumNewSourceMessages)
                throw new InvalidOperationException("至少需要一轮尚未压缩的完整对话。");

            var sourceMessages = new List<CopilotRequestMessage>();
            var remainingWeight = limits.MaximumCharacters - compactRequestWeight;
            var terminalConstraintWeight = 0L;
            if (existingCompaction != null)
            {
                var summaryMessage = CopilotConversationCompactionContext.CreateSummaryMessage(existingCompaction);
                var summaryWeight = CopilotTokenEstimator.EstimateTextWeight(summaryMessage.Content);
                if (summaryWeight > remainingWeight)
                    throw new InvalidOperationException("现有延续摘要已超过当前模型可安全处理的上下文空间。");

                sourceMessages.Add(summaryMessage);
                remainingWeight -= summaryWeight;

                var terminalConstraint = CopilotConversationCompactionTerminalEvidence
                    .Capture(conversation.Messages.Take(startIndex))
                    .BuildMissingSourceConstraint(existingCompaction.Summary);
                if (terminalConstraint.Length > 0)
                {
                    terminalConstraintWeight = CopilotTokenEstimator.EstimateTextWeight(terminalConstraint);
                    if (terminalConstraintWeight > remainingWeight)
                        throw new InvalidOperationException("当前模型没有足够的上下文空间来保留原始对话的终态证据。");

                    sourceMessages.Add(new CopilotRequestMessage("user", terminalConstraint));
                    remainingWeight -= terminalConstraintWeight;
                }
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
                var contentWeight = CopilotTokenEstimator.EstimateTextWeight(content);
                if (contentWeight > remainingWeight)
                    break;

                sourceMessages.Add(new CopilotRequestMessage(message.IsUser ? "user" : "assistant", content));
                remainingWeight -= contentWeight;
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
            var boundaryMessage = pendingMessages[lastCompleteTurnCount - 1];
            var boundaryIndex = conversation.Messages.IndexOf(boundaryMessage);
            if (boundaryIndex < 0)
                throw new InvalidOperationException("压缩边界已不在当前会话中，原有摘要和聊天记录均未改变。");

            return new CopilotConversationCompactionPlan(
                sourceMessages.ToArray(),
                boundaryMessage,
                CopilotConversationCompactionTerminalEvidence.Capture(
                    conversation.Messages.Take(boundaryIndex + 1)),
                lastCompleteTurnCount,
                lastCompleteTurnCharacters,
                SaturatingAdd(previousMessageCount, lastCompleteTurnCount),
                SaturatingAdd(previousCharacters, lastCompleteTurnCharacters),
                sourceMessages.Sum(message =>
                    (long)CopilotTokenEstimator.EstimateTextWeight(message.Content)) - terminalConstraintWeight,
                CreateSourceRevision(conversation, boundaryMessage.Id));
        }

        internal static string CreateSourceRevision(
            CopilotConversationRecord conversation,
            string boundaryMessageId)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            if (string.IsNullOrWhiteSpace(boundaryMessageId))
                return string.Empty;

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendRevisionField(hash, "copilot-compaction-source-v1");
            var compaction = ResolveExistingCompaction(conversation, out _);
            AppendRevisionField(hash, compaction == null ? "none" : "present");
            if (compaction != null)
            {
                AppendRevisionField(hash, compaction.StrategyVersion.ToString(
                    CultureInfo.InvariantCulture));
                AppendRevisionField(hash, compaction.Summary);
                AppendRevisionField(hash, compaction.ThroughMessageId);
                AppendRevisionField(hash, compaction.CreatedAtUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture));
                AppendRevisionField(hash, compaction.SourceMessageCount.ToString(
                    CultureInfo.InvariantCulture));
                AppendRevisionField(hash, compaction.SourceCharacters.ToString(
                    CultureInfo.InvariantCulture));
            }

            foreach (var message in conversation.Messages)
            {
                AppendRevisionField(hash, message.Id);
                AppendRevisionField(hash, message.IsUser ? "user" : "assistant");
                AppendRevisionField(hash, message.ModelContent);
                if (string.Equals(
                        message.Id,
                        boundaryMessageId,
                        StringComparison.Ordinal))
                {
                    return Convert.ToHexString(hash.GetHashAndReset());
                }
            }

            return string.Empty;
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

        private static void AppendRevisionField(
            IncrementalHash hash,
            string? value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }
    }
}
