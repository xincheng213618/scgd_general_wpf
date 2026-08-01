using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed record CopilotConversationRewindPoint(
        int Ordinal,
        CopilotChatMessage UserMessage,
        string Preview,
        int AttachmentCount);

    internal static class CopilotConversationRewindService
    {
        private const int MaximumListedPoints = 10;
        private const int MaximumPreviewCharacters = 72;

        public static IReadOnlyList<CopilotConversationRewindPoint> GetPoints(
            CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return Array.Empty<CopilotConversationRewindPoint>();

            var points = new List<CopilotConversationRewindPoint>();
            for (var index = conversation.Messages.Count - 1; index >= 0; index--)
            {
                var message = conversation.Messages[index];
                if (message?.IsUser != true || string.IsNullOrWhiteSpace(message.Content))
                    continue;

                points.Add(new CopilotConversationRewindPoint(
                    points.Count + 1,
                    message,
                    BuildPreview(message.Content),
                    message.AttachmentSnapshotCaptured ? message.Attachments.Count : 0));
            }
            return points;
        }

        public static bool TryResolve(
            CopilotConversationRecord? conversation,
            string? requestedOrdinal,
            out CopilotConversationRewindPoint point)
        {
            point = null!;
            if (!int.TryParse(
                    (requestedOrdinal ?? string.Empty).Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var ordinal)
                || ordinal <= 0)
            {
                return false;
            }

            point = GetPoints(conversation).FirstOrDefault(candidate => candidate.Ordinal == ordinal)!;
            return point != null;
        }

        public static string Format(CopilotConversationRecord? conversation)
        {
            var points = GetPoints(conversation);
            if (points.Count == 0)
                return "当前会话还没有可回溯的用户请求。";

            var builder = new StringBuilder();
            builder.Append("会话回溯点 · ")
                .Append(points.Count.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine();
            builder.AppendLine();
            builder.AppendLine("输入 /rewind N 创建仅包含该请求之前历史的新会话分支，并把原请求恢复到输入框；1 表示最近一条。");
            foreach (var point in points.Take(MaximumListedPoints))
            {
                builder.Append(point.Ordinal.ToString(CultureInfo.InvariantCulture))
                    .Append(" · ")
                    .Append(point.Preview);
                if (point.AttachmentCount > 0)
                    builder.Append(" · 附件 ").Append(point.AttachmentCount.ToString("N0", CultureInfo.CurrentCulture));
                builder.AppendLine();
            }
            if (points.Count > MaximumListedPoints)
            {
                builder.Append("…另有 ")
                    .Append((points.Count - MaximumListedPoints).ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 个更早请求，可直接输入对应序号。");
            }

            builder.AppendLine();
            builder.Append("源会话、当前文件和外部操作保持不变；Agent checkpoint 与临时授权不会进入回溯分支。");
            return builder.ToString();
        }

        private static string BuildPreview(string? content)
        {
            var normalized = string.Join(
                " ",
                (content ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return normalized.Length <= MaximumPreviewCharacters
                ? normalized
                : normalized[..MaximumPreviewCharacters].TrimEnd() + "…";
        }
    }
}
