using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotConversationSearchPreview
    {
        internal const int MaximumPreviewCharacters = 96;

        internal static bool TryBuild(
            CopilotConversationRecord conversation,
            IReadOnlyList<string> terms,
            out string preview)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentNullException.ThrowIfNull(terms);

            var normalizedTerms = terms
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Select(term => term.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedTerms.Length == 0)
            {
                preview = string.Empty;
                return false;
            }

            var sources = EnumerateSources(conversation).ToArray();
            var matchingSources = normalizedTerms
                .Select(term => sources.Where(source => source.Contains(term)).ToArray())
                .ToArray();
            if (matchingSources.Any(matches => matches.Length == 0))
            {
                preview = string.Empty;
                return false;
            }

            var singleSource = sources.FirstOrDefault(source =>
                normalizedTerms.All(source.Contains));
            if (singleSource != null)
            {
                preview = BuildPreview(singleSource.Label, singleSource.DisplayText);
                return true;
            }

            var labels = matchingSources
                .Select(matches => matches[0].Label)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            preview = BuildPreview("匹配", string.Join("、", labels));
            return true;
        }

        private static IEnumerable<SearchSource> EnumerateSources(
            CopilotConversationRecord conversation)
        {
            yield return new SearchSource("标题", conversation.Title, conversation.Title);
            yield return new SearchSource("草稿", conversation.DraftText, conversation.DraftText);
            yield return new SearchSource(
                "暂存",
                conversation.ComposerStash?.Text,
                conversation.ComposerStash?.Text);
            yield return new SearchSource(
                "目标",
                conversation.Goal?.Objective,
                conversation.Goal?.Objective);
            yield return new SearchSource("最近消息", conversation.PreviewText, conversation.PreviewText);

            foreach (var message in (conversation.Messages ?? []).Reverse())
            {
                if (message == null)
                    continue;

                yield return new SearchSource("历史消息", message.Content, message.Content);
                foreach (var attachment in message.Attachments ?? [])
                    yield return BuildAttachmentSource("消息附件", attachment);
            }

            foreach (var attachment in conversation.Attachments ?? [])
                yield return BuildAttachmentSource("附件", attachment);

            foreach (var attachment in conversation.ComposerStash?.Attachments ?? [])
                yield return BuildAttachmentSource("暂存附件", attachment);

            yield return new SearchSource(
                "模型",
                conversation.ProfileDisplayName,
                conversation.ProfileDisplayName);
        }

        private static SearchSource BuildAttachmentSource(
            string label,
            CopilotAttachmentItem? attachment)
        {
            if (attachment == null)
                return new SearchSource(label, string.Empty, string.Empty);

            var searchable = new List<string>
            {
                attachment.Title,
                attachment.DisplayLabel,
                attachment.Source,
            };
            if (attachment.Type is CopilotAttachmentType.File or CopilotAttachmentType.Image)
                searchable.Add(attachment.Value);

            return new SearchSource(
                label,
                string.Join(Environment.NewLine, searchable.Where(value => !string.IsNullOrWhiteSpace(value))),
                attachment.DisplayLabel);
        }

        private static string BuildPreview(string label, string? value)
        {
            var normalizedValue = NormalizeSingleLine(value);
            var combined = normalizedValue.Length == 0
                ? label
                : $"{label} · {normalizedValue}";
            if (combined.Length <= MaximumPreviewCharacters)
                return combined;

            var retainedLength = MaximumPreviewCharacters;
            if (char.IsHighSurrogate(combined[retainedLength - 1])
                && char.IsLowSurrogate(combined[retainedLength]))
            {
                retainedLength--;
            }
            return combined[..retainedLength].TrimEnd() + "…";
        }

        private static string NormalizeSingleLine(string? value) =>
            string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        private sealed record SearchSource(
            string Label,
            string? SearchText,
            string? DisplayText)
        {
            internal bool Contains(string term) =>
                !string.IsNullOrWhiteSpace(SearchText)
                && SearchText.Contains(term, StringComparison.OrdinalIgnoreCase);
        }
    }
}
