using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ColorVision.Copilot
{
    internal static class CopilotConversationMarkdownExporter
    {
        internal sealed record AttachmentSnapshot(
            CopilotAttachmentType Type,
            string Title,
            string Value,
            string Source,
            string DisplayLabel);

        internal sealed record MessageSnapshot(
            bool IsUser,
            string Header,
            DateTime CreatedAt,
            string Content,
            string ResponseInterruptionText,
            IReadOnlyList<AttachmentSnapshot> Attachments);

        internal sealed record Snapshot(
            string Title,
            DateTime ExportedAt,
            DateTime CreatedAt,
            DateTime UpdatedAt,
            string ProfileName,
            IReadOnlyList<MessageSnapshot> Messages);

        private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        public static bool CanExport(CopilotConversationRecord? conversation)
        {
            return conversation?.Messages.Any(IsExportableMessage) == true;
        }

        public static string BuildFileName(CopilotConversationRecord conversation)
        {
            ArgumentNullException.ThrowIfNull(conversation);

            var title = string.Join(" ", (conversation.Title ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
                title = title.Replace(invalidCharacter, '_');

            title = title.Trim(' ', '.');
            if (title.Length > 64)
                title = title[..64].TrimEnd(' ', '.');
            if (string.IsNullOrWhiteSpace(title))
                title = "Copilot 会话";
            if (ReservedFileNames.Contains(Path.GetFileNameWithoutExtension(title)))
                title = "_" + title;

            return $"{title}-{conversation.UpdatedAt:yyyyMMdd-HHmm}.md";
        }

        public static string BuildMarkdown(CopilotConversationRecord conversation)
        {
            return BuildMarkdown(Capture(conversation), CancellationToken.None);
        }

        public static Snapshot Capture(CopilotConversationRecord conversation)
        {
            ArgumentNullException.ThrowIfNull(conversation);

            var messages = conversation.Messages
                .Where(IsExportableMessage)
                .Select(message => new MessageSnapshot(
                    message.IsUser,
                    message.Header ?? string.Empty,
                    message.CreatedAt,
                    message.Content ?? string.Empty,
                    message.HasResponseInterruption ? message.ResponseInterruptionText : string.Empty,
                    message.Attachments
                        .Where(attachment => attachment != null)
                        .Select(attachment => new AttachmentSnapshot(
                            attachment.Type,
                            attachment.Title ?? string.Empty,
                            attachment.Value ?? string.Empty,
                            attachment.Source ?? string.Empty,
                            attachment.DisplayLabel ?? string.Empty))
                        .ToArray()))
                .ToArray();

            return new Snapshot(
                conversation.Title ?? string.Empty,
                DateTime.Now,
                conversation.CreatedAt,
                conversation.UpdatedAt,
                ResolveProfileName(conversation),
                messages);
        }

        public static string BuildMarkdown(Snapshot snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            cancellationToken.ThrowIfCancellationRequested();

            var builder = new StringBuilder();
            builder.Append("# ").AppendLine(EscapeMarkdownText(snapshot.Title));
            builder.AppendLine();
            builder.Append("- 导出时间：").AppendLine(snapshot.ExportedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.Append("- 会话创建：").AppendLine(snapshot.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.Append("- 最近更新：").AppendLine(snapshot.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.Append("- 模型：").AppendLine(EscapeMarkdownText(snapshot.ProfileName));
            builder.AppendLine();
            builder.AppendLine("> 仅导出对话中可见的正文和附件引用；内部推理、隐藏执行轨迹及未发送的输入不会写入此文件。");

            foreach (var message in snapshot.Messages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.AppendLine();
                var speaker = message.IsUser ? "用户" : string.IsNullOrWhiteSpace(message.Header) ? "AI" : message.Header;
                builder.Append("## ").Append(EscapeMarkdownText(speaker)).Append(" · ").AppendLine(message.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.AppendLine();

                if (!string.IsNullOrWhiteSpace(message.Content))
                {
                    builder.AppendLine(message.Content.Trim());
                    builder.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(message.ResponseInterruptionText))
                {
                    builder.Append("> ⚠️ ").AppendLine(EscapeMarkdownText(message.ResponseInterruptionText));
                    builder.AppendLine();
                }

                if (message.Attachments.Count == 0)
                    continue;

                builder.AppendLine("### 附件");
                builder.AppendLine();
                foreach (var attachment in message.Attachments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendAttachment(builder, attachment);
                }
            }

            return builder.ToString().TrimEnd() + Environment.NewLine;
        }

        private static bool IsExportableMessage(CopilotChatMessage message)
        {
            return !message.IsResponsePending
                && (!string.IsNullOrWhiteSpace(message.Content) || message.HasAttachments);
        }

        private static string ResolveProfileName(CopilotConversationRecord conversation)
        {
            if (!string.IsNullOrWhiteSpace(conversation.ProfileDisplayName))
                return conversation.ProfileDisplayName;
            if (!string.IsNullOrWhiteSpace(conversation.ProfileId))
                return conversation.ProfileId;
            return "未记录";
        }

        private static void AppendAttachment(StringBuilder builder, AttachmentSnapshot attachment)
        {
            var typeLabel = attachment.Type switch
            {
                CopilotAttachmentType.File => "文件",
                CopilotAttachmentType.Image => "图片",
                CopilotAttachmentType.WebPage => "网页",
                _ => "上下文",
            };
            var title = ResolveAttachmentTitle(attachment, typeLabel);
            builder.Append("- **").Append(typeLabel).Append("**：").Append(EscapeMarkdownText(title));

            if (attachment.Type == CopilotAttachmentType.WebPage && TryNormalizeWebAddress(attachment.Source, out var webAddress))
            {
                builder.Append(" — <").Append(webAddress).Append('>');
            }
            else
            {
                var reference = attachment.Type == CopilotAttachmentType.Context ? attachment.Source : attachment.Value;
                if (!string.IsNullOrWhiteSpace(reference))
                    builder.Append(" — `").Append(NormalizeInlineCode(reference)).Append('`');
            }

            builder.AppendLine();
        }

        private static string ResolveAttachmentTitle(AttachmentSnapshot attachment, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(attachment.Title))
                return attachment.Title;
            if (!string.IsNullOrWhiteSpace(attachment.DisplayLabel))
                return attachment.DisplayLabel;
            if ((attachment.Type == CopilotAttachmentType.File || attachment.Type == CopilotAttachmentType.Image)
                && !string.IsNullOrWhiteSpace(attachment.Value))
            {
                return Path.GetFileName(attachment.Value);
            }
            if (attachment.Type == CopilotAttachmentType.WebPage
                && Uri.TryCreate(attachment.Source, UriKind.Absolute, out var uri)
                && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri.Host;
            }
            return fallback;
        }

        private static bool TryNormalizeWebAddress(string? source, out string address)
        {
            address = string.Empty;
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            address = uri.AbsoluteUri.Replace(">", "%3E", StringComparison.Ordinal);
            return true;
        }

        private static string NormalizeInlineCode(string text)
        {
            return text.Trim()
                .Replace('`', '′')
                .Replace('\r', ' ')
                .Replace('\n', ' ');
        }

        private static string EscapeMarkdownText(string? text)
        {
            return (text ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("#", "\\#", StringComparison.Ordinal)
                .Replace("*", "\\*", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal)
                .Replace("[", "\\[", StringComparison.Ordinal)
                .Replace("]", "\\]", StringComparison.Ordinal)
                .Replace("`", "\\`", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();
        }
    }
}
