using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotFeedbackDraft(
        string Report,
        string ConversationMarkdown,
        int IncludedMessageCount,
        int OmittedMessageCount)
    {
        public bool HasConversationAttachment => ConversationMarkdown.Length > 0;
    }

    internal static class CopilotFeedbackDraftBuilder
    {
        public const int MaximumReportCharacters = 4_000;
        public const int MaximumConversationCharacters = 200_000;
        public const int MaximumMessages = 50;

        private const int MaximumMessageContentCharacters = 32_000;
        private const int MaximumInterruptionCharacters = 2_048;
        private const int MaximumAttachmentFieldCharacters = 1_024;
        private const int MaximumAttachmentsPerMessage = 24;

        public static CopilotFeedbackDraft Create(
            CopilotConversationRecord? conversation,
            string? report)
        {
            var normalizedReport = NormalizeReport(report);
            if (!CopilotConversationMarkdownExporter.CanExport(conversation))
                return new CopilotFeedbackDraft(normalizedReport, string.Empty, 0, 0);

            var snapshot = CopilotConversationMarkdownExporter.Capture(conversation!);
            var messages = snapshot.Messages
                .TakeLast(MaximumMessages)
                .Select(BoundMessage)
                .ToList();
            var omittedMessageCount = snapshot.Messages.Count - messages.Count;

            string markdown;
            while (true)
            {
                markdown = BuildMarkdown(snapshot, messages, omittedMessageCount);
                if (markdown.Length <= MaximumConversationCharacters || messages.Count <= 1)
                    break;

                messages.RemoveAt(0);
                omittedMessageCount++;
            }
            markdown = BoundText(markdown, MaximumConversationCharacters);

            return new CopilotFeedbackDraft(
                normalizedReport,
                markdown,
                messages.Count,
                omittedMessageCount);
        }

        public static string NormalizeReport(string? report)
        {
            return BoundText((report ?? string.Empty).Trim(), MaximumReportCharacters);
        }

        private static CopilotConversationMarkdownExporter.MessageSnapshot BoundMessage(
            CopilotConversationMarkdownExporter.MessageSnapshot message)
        {
            return message with
            {
                Content = BoundText(message.Content, MaximumMessageContentCharacters),
                ResponseInterruptionText = BoundText(
                    message.ResponseInterruptionText,
                    MaximumInterruptionCharacters),
                Attachments = message.Attachments
                    .Take(MaximumAttachmentsPerMessage)
                    .Select(attachment => attachment with
                    {
                        Title = BoundText(attachment.Title, MaximumAttachmentFieldCharacters),
                        Value = BoundText(attachment.Value, MaximumAttachmentFieldCharacters),
                        Source = BoundText(attachment.Source, MaximumAttachmentFieldCharacters),
                        DisplayLabel = BoundText(attachment.DisplayLabel, MaximumAttachmentFieldCharacters),
                    })
                    .ToArray(),
            };
        }

        private static string BuildMarkdown(
            CopilotConversationMarkdownExporter.Snapshot snapshot,
            List<CopilotConversationMarkdownExporter.MessageSnapshot> messages,
            int omittedMessageCount)
        {
            var boundedSnapshot = snapshot with { Messages = messages };
            var markdown = CopilotConversationMarkdownExporter.BuildMarkdown(boundedSnapshot);
            var notice = omittedMessageCount > 0
                ? $"> `/feedback` 已附加最近 {messages.Count:N0} 条可见消息；较早的 {omittedMessageCount:N0} 条因反馈大小限制未附加。正文和附件引用也会有界处理；提交前可在反馈窗口移除此附件。"
                : $"> `/feedback` 已附加当前会话的 {messages.Count:N0} 条可见消息。正文和附件引用会按反馈大小限制有界处理；提交前可在反馈窗口移除此附件。";
            var firstLineEnd = markdown.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            if (firstLineEnd < 0)
                return notice + Environment.NewLine + markdown;

            return markdown.Insert(
                firstLineEnd + Environment.NewLine.Length,
                Environment.NewLine + notice + Environment.NewLine);
        }

        private static string BoundText(string? value, int maximumCharacters)
        {
            var text = value ?? string.Empty;
            if (text.Length <= maximumCharacters)
                return text;

            var retainedLength = maximumCharacters - 1;
            if (retainedLength > 0
                && char.IsHighSurrogate(text[retainedLength - 1])
                && char.IsLowSurrogate(text[retainedLength]))
            {
                retainedLength--;
            }
            return text[..retainedLength] + "…";
        }
    }
}
