using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotConversationHistorySnapshot
    {
        public static CopilotConversationHistorySnapshot Empty { get; } = new([], []);

        public IReadOnlyList<CopilotRequestMessage> ModelMessages { get; }

        public IReadOnlyList<CopilotRequestMessage> VisibleMessages { get; }

        public CopilotConversationHistorySnapshot(
            IEnumerable<CopilotRequestMessage>? modelMessages,
            IEnumerable<CopilotRequestMessage>? visibleMessages)
        {
            ModelMessages = (modelMessages ?? Array.Empty<CopilotRequestMessage>()).ToArray();
            VisibleMessages = (visibleMessages ?? Array.Empty<CopilotRequestMessage>()).ToArray();
        }
    }

    public sealed class CopilotConversationRequestBuilder
    {
        public const int AttachmentContentLimit = 12_000;
        public const int MaximumWebContextCharacters = 8_000;

        private readonly Func<string, CancellationToken, Task<CopilotFetchedWebPageContent>> _loadWebPage;

        public CopilotConversationRequestBuilder()
            : this(CopilotWebPageToolSupport.LoadWebPageContentAsync)
        {
        }

        public CopilotConversationRequestBuilder(Func<string, CancellationToken, Task<CopilotFetchedWebPageContent>> loadWebPage)
        {
            _loadWebPage = loadWebPage ?? throw new ArgumentNullException(nameof(loadWebPage));
        }

        public static CopilotConversationHistoryLimits ResolveHistoryLimits(int contextWindowTokens, int maxOutputTokens) =>
            CopilotConversationHistoryWindow.ResolveLimits(contextWindowTokens, maxOutputTokens);

        public static IReadOnlyList<CopilotRequestMessage> BuildChatHistory(
            CopilotConversationHistorySnapshot historySnapshot,
            string? currentUserModelContent,
            IEnumerable<CopilotAttachmentItem>? attachments,
            CopilotConversationHistoryLimits limits,
            bool includeAttachmentContext)
        {
            ArgumentNullException.ThrowIfNull(historySnapshot);
            var history = historySnapshot.ModelMessages
                .Append(new CopilotRequestMessage("user", (currentUserModelContent ?? string.Empty).Trim()))
                .ToArray();
            var attachmentContext = includeAttachmentContext ? BuildAttachmentContextBlock(attachments) : string.Empty;
            if (string.IsNullOrWhiteSpace(attachmentContext))
                return CopilotConversationHistoryWindow.Select(history, limits);

            var attachment = CopilotConversationHistoryWindow.Select(
                    [new CopilotRequestMessage("user", attachmentContext)],
                    maximumMessages: 1,
                    maximumCharacters: limits.MaximumContentCharacters,
                    maximumContentCharacters: limits.MaximumContentCharacters)
                .Single();
            var selected = CopilotConversationHistoryWindow.Select(
                    history,
                    Math.Max(1, limits.MaximumMessages - 1),
                    Math.Max(1, limits.MaximumCharacters - attachment.Content.Length),
                    limits.MaximumContentCharacters)
                .ToList();
            selected.Insert(0, attachment);
            return selected;
        }

        public static IReadOnlyList<CopilotRequestMessage> BuildVisibleHistory(
            CopilotConversationHistorySnapshot historySnapshot,
            CopilotConversationHistoryLimits limits)
        {
            ArgumentNullException.ThrowIfNull(historySnapshot);
            return CopilotConversationHistoryWindow.Select(historySnapshot.VisibleMessages, limits);
        }

        public static CopilotConversationHistorySnapshot CaptureHistorySnapshot(
            CopilotConversationRecord conversation,
            CopilotChatMessage? stopBeforeMessage = null)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            return new CopilotConversationHistorySnapshot(
                CopilotConversationCompactionContext.Build(conversation, stopBeforeMessage, useModelContent: true),
                CopilotConversationCompactionContext.Build(conversation, stopBeforeMessage, useModelContent: false));
        }

        public static CopilotConversationHistorySelection CaptureHistorySelection(
            CopilotConversationRecord? conversation,
            CopilotConversationHistoryLimits limits)
        {
            var history = conversation == null
                ? Array.Empty<CopilotRequestMessage>()
                : CopilotConversationCompactionContext.Build(conversation, stopBeforeMessage: null, useModelContent: true);
            return CopilotConversationHistoryWindow.SelectWithDiagnostics(history, limits);
        }

        public static string BuildAttachmentContextBlock(IEnumerable<CopilotAttachmentItem>? attachments)
        {
            var available = (attachments ?? Array.Empty<CopilotAttachmentItem>())
                .Where(attachment => attachment != null)
                .ToArray();
            if (available.Length == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("The following context is attached to the current chat. It was explicitly provided by the user; use it when relevant.");

            foreach (var attachment in available)
            {
                builder.AppendLine(attachment.Type switch
                {
                    CopilotAttachmentType.File => BuildFileAttachmentBlock(attachment),
                    CopilotAttachmentType.Image => BuildImageAttachmentBlock(attachment),
                    CopilotAttachmentType.WebPage => BuildWebPageAttachmentBlock(attachment),
                    _ => BuildContextAttachmentBlock(attachment),
                });
            }

            return builder.ToString().Trim();
        }

        public static string BuildContextAttachmentContent(IReadOnlyList<CopilotContextItem>? contextItems)
        {
            if (contextItems == null || contextItems.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("The following business snapshots were explicitly attached by the user. They are fixed snapshots captured by the app or attached manually; prioritize them when answering.")
                .AppendLine();

            foreach (var item in contextItems)
            {
                if (item == null)
                    continue;

                var title = string.IsNullOrWhiteSpace(item.Title) ? CopilotUiText.ContextBadge : item.Title.Trim();
                builder.Append("## ").AppendLine(title);
                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.Append("Summary: ").AppendLine(item.Summary.Trim());
                if (!string.IsNullOrWhiteSpace(item.Content))
                    builder.AppendLine(item.Content.Trim());
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }

        public async Task<string> BuildUserRequestContentAsync(
            string? prompt,
            CopilotLiveContext? liveContext,
            CancellationToken cancellationToken)
        {
            var normalizedPrompt = (prompt ?? string.Empty).Trim();
            var builder = new StringBuilder();
            AppendLiveContextSummaryBlock(builder, liveContext);

            if (builder.Length > 0 && normalizedPrompt.Length > 0)
                builder.AppendLine();
            builder.Append(normalizedPrompt);

            var urls = CopilotWebPageToolSupport.ExtractHttpUrls(normalizedPrompt);
            if (urls.Count == 0)
                return builder.ToString().Trim();

            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("[Local Web Context Injection]");
            builder.AppendLine("The following web page content was fetched locally before sending. Answer web-page questions only from these fetched results. If fetching failed or the fetched content lacks relevant information, say so explicitly and do not assume unseen page content.");

            var remainingCharacters = MaximumWebContextCharacters;
            foreach (var url in urls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var contextBlock = await BuildWebPageContextBlockAsync(url, cancellationToken).ConfigureAwait(false);
                if (contextBlock.Length > remainingCharacters)
                {
                    builder.AppendLine();
                    builder.Append(contextBlock[..remainingCharacters]);
                    builder.AppendLine();
                    builder.AppendLine("...<web context truncated>");
                    break;
                }

                builder.AppendLine();
                builder.AppendLine(contextBlock);
                remainingCharacters -= contextBlock.Length;
                if (remainingCharacters > 0)
                    continue;

                builder.AppendLine();
                builder.AppendLine("...<web context truncated>");
                break;
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendLiveContextSummaryBlock(StringBuilder builder, CopilotLiveContext? liveContext)
        {
            if (liveContext == null
                || string.IsNullOrWhiteSpace(liveContext.Title) && string.IsNullOrWhiteSpace(liveContext.Summary))
            {
                return;
            }

            builder.AppendLine("[Current Window Context]");
            if (!string.IsNullOrWhiteSpace(liveContext.Title))
                builder.Append("Location: ").AppendLine(liveContext.Title.Trim());
            if (!string.IsNullOrWhiteSpace(liveContext.Summary))
                builder.Append("Summary: ").AppendLine(liveContext.Summary.Trim());
            builder.AppendLine("This is a lightweight summary of the current business window. If explicit snapshots are also attached, prioritize those snapshots when answering.");
        }

        private async Task<string> BuildWebPageContextBlockAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var page = await _loadWebPage(url, cancellationToken).ConfigureAwait(false);
                return CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(page);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return CopilotWebPageToolSupport.BuildFailedWebPageContextBlock(url, CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
        }

        private static string BuildFileAttachmentBlock(CopilotAttachmentItem attachment)
        {
            try
            {
                if (!File.Exists(attachment.Value))
                    return $"[{CopilotUiText.FileBadge}] {attachment.Value}\nThe file does not exist and cannot be read.\n";

                var content = ReadBoundedTextFile(attachment.Value, out var wasTruncated);
                if (wasTruncated)
                    content += "\n...<truncated>";
                var fence = ResolveCodeFence(attachment.Value);
                return $"[{CopilotUiText.FileBadge}] {attachment.Value}\n~~~{fence}\n{content}\n~~~\n";
            }
            catch (Exception ex)
            {
                return $"[{CopilotUiText.FileBadge}] {attachment.Value}\nRead failed: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}\n";
            }
        }

        private static string BuildImageAttachmentBlock(CopilotAttachmentItem attachment)
        {
            if (!File.Exists(attachment.Value))
                return $"[{CopilotUiText.ImageBadge}] {attachment.DisplayLabel}\nThe local image attachment does not exist: {attachment.Value}\n";

            return string.Join(Environment.NewLine,
            [
                $"[{CopilotUiText.ImageBadge}] {attachment.DisplayLabel}",
                $"Local image path: {attachment.Value}",
                "The current version shows image previews in the UI but does not automatically upload pixel content to the model.",
                string.Empty,
            ]);
        }

        private static string BuildWebPageAttachmentBlock(CopilotAttachmentItem attachment)
        {
            var content = attachment.Value ?? string.Empty;
            if (content.Length > AttachmentContentLimit)
                content = content[..AttachmentContentLimit] + "\n...<truncated>";

            return string.Join(Environment.NewLine,
            [
                $"[{CopilotUiText.WebPageBadge}] {attachment.DisplayLabel}",
                $"Source: {attachment.Source}",
                content,
                string.Empty,
            ]);
        }

        private static string BuildContextAttachmentBlock(CopilotAttachmentItem attachment)
        {
            return $"[{CopilotUiText.ContextBadge}] {attachment.DisplayLabel}{Environment.NewLine}"
                + attachment.Value
                + Environment.NewLine;
        }

        private static string ReadBoundedTextFile(string filePath, out bool wasTruncated)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[AttachmentContentLimit + 1];
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = reader.ReadBlock(buffer, totalRead, buffer.Length - totalRead);
                if (read == 0)
                    break;
                totalRead += read;
            }

            wasTruncated = totalRead > AttachmentContentLimit || reader.Peek() >= 0;
            return new string(buffer, 0, Math.Min(totalRead, AttachmentContentLimit));
        }

        private static string ResolveCodeFence(string filePath)
        {
            var extension = Path.GetExtension(filePath).Trim().TrimStart('.');
            return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
        }
    }
}
