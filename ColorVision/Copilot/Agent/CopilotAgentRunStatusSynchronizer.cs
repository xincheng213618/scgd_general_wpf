using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public static class CopilotAgentRunStatusSynchronizer
    {
        public static void Refresh(
            IEnumerable<CopilotConversationRecord> conversations,
            string? activeConversationId,
            CopilotHostedRunState? activeState,
            IReadOnlyList<string>? queuedConversationIds)
        {
            ArgumentNullException.ThrowIfNull(conversations);

            var normalizedActiveId = activeConversationId?.Trim() ?? string.Empty;
            var queuePositions = (queuedConversationIds ?? Array.Empty<string>())
                .Select((conversationId, index) => new { ConversationId = conversationId?.Trim(), Position = index + 1 })
                .Where(item => !string.IsNullOrWhiteSpace(item.ConversationId))
                .GroupBy(item => item.ConversationId!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Position, StringComparer.Ordinal);

            foreach (var conversation in conversations)
            {
                if (conversation == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(normalizedActiveId)
                    && string.Equals(conversation.Id, normalizedActiveId, StringComparison.Ordinal))
                {
                    conversation.AgentRunStatusLabel = FormatActiveState(activeState);
                    continue;
                }

                conversation.AgentRunStatusLabel = queuePositions.TryGetValue(conversation.Id, out var position)
                    ? $"排队 {position}"
                    : string.Empty;
            }
        }

        public static string FormatActiveState(CopilotHostedRunState? state)
        {
            return state switch
            {
                CopilotHostedRunState.Running => "运行中",
                CopilotHostedRunState.PauseRequested => "暂停中",
                CopilotHostedRunState.CancelRequested => "取消中",
                _ => string.Empty,
            };
        }
    }
}
