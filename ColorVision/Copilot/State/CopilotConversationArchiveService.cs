using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotConversationArchiveService
    {
        private const int MaximumListedConversations = 30;

        public static IReadOnlyList<CopilotConversationRecord> GetActive(
            IEnumerable<CopilotConversationRecord>? conversations)
        {
            return (conversations ?? [])
                .Where(conversation => conversation != null && !conversation.IsArchived)
                .ToArray();
        }

        public static CopilotConversationRecord? FindUniqueArchived(
            IEnumerable<CopilotConversationRecord>? conversations,
            string? query)
        {
            var normalizedQuery = (query ?? string.Empty).Trim();
            if (normalizedQuery.Length == 0)
                return null;

            var archived = (conversations ?? [])
                .Where(conversation => conversation?.IsArchived == true)
                .ToArray();
            var idMatch = archived.FirstOrDefault(conversation => string.Equals(
                conversation.Id,
                normalizedQuery,
                StringComparison.Ordinal));
            if (idMatch != null)
                return idMatch;

            var titleMatches = archived
                .Where(conversation => string.Equals(
                    conversation.Title.Trim(),
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            return titleMatches.Length == 1 ? titleMatches[0] : null;
        }

        public static string FormatArchived(
            IEnumerable<CopilotConversationRecord>? conversations,
            string? query = null)
        {
            var normalizedQuery = (query ?? string.Empty).Trim();
            var archived = (conversations ?? [])
                .Where(conversation => conversation?.IsArchived == true)
                .Where(conversation => normalizedQuery.Length == 0
                    || conversation.Id.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || conversation.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || conversation.PreviewText.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(conversation => conversation.UpdatedAt)
                .Take(MaximumListedConversations + 1)
                .ToArray();
            if (archived.Length == 0)
            {
                return normalizedQuery.Length == 0
                    ? "没有已归档会话。"
                    : $"没有匹配“{normalizedQuery}”的已归档会话。";
            }

            var shown = archived.Take(MaximumListedConversations).ToArray();
            var builder = new StringBuilder();
            builder.Append("已归档会话 · ").Append(shown.Length);
            if (archived.Length > shown.Length)
                builder.Append('+');
            builder.AppendLine();
            foreach (var conversation in shown)
            {
                builder.Append("- ")
                    .AppendLine(conversation.Title)
                    .Append("  ")
                    .Append(conversation.Id)
                    .Append(" · ")
                    .AppendLine(conversation.UpdatedLabel);
            }
            builder.AppendLine();
            builder.Append("使用 /unarchive <会话 ID 或唯一完整标题> 恢复。");
            return builder.ToString().TrimEnd();
        }
    }
}
