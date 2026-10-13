using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal enum CopilotMarkdownInlineKind
    {
        Bold,
        Code,
        Italic,
        Link,
    }

    internal readonly record struct CopilotMarkdownInlineToken(
        CopilotMarkdownInlineKind Kind,
        int Start,
        int Length,
        int ContentStart,
        int ContentLength);

    internal static class CopilotMarkdownInlineScanner
    {
        public static IReadOnlyList<CopilotMarkdownInlineToken> Parse(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            var index = text.AsSpan().IndexOfAny("*`[<");
            if (index < 0)
                return Array.Empty<CopilotMarkdownInlineToken>();

            Dictionary<int, int>? codeClosers = null;
            Dictionary<int, int>? linkTargetClosers = null;
            var hasLinks = text.IndexOf('[', index) >= 0;
            if (hasLinks || text.IndexOf('`', index) >= 0)
                IndexDelimiters(text, hasLinks, out codeClosers, out linkTargetClosers);

            List<CopilotMarkdownInlineToken>? tokens = null;
            var nextStarBoundary = -1;
            var nextLinkLabelBoundary = -1;
            while (index < text.Length)
            {
                CopilotMarkdownInlineToken token;
                if (text[index] == '`')
                {
                    var delimiterEnd = index + 1;
                    while (delimiterEnd < text.Length && text[delimiterEnd] == '`')
                        delimiterEnd++;

                    if (codeClosers == null || !codeClosers.TryGetValue(index, out var closingStart))
                    {
                        index = delimiterEnd;
                        continue;
                    }

                    var delimiterLength = delimiterEnd - index;
                    token = new(CopilotMarkdownInlineKind.Code, index, closingStart + delimiterLength - index,
                        delimiterEnd, closingStart - delimiterEnd);
                }
                else
                {
                    token = text[index] switch
                    {
                        '*' => ReadEmphasis(text, index, ref nextStarBoundary),
                        '[' => ReadLink(text, index, linkTargetClosers, ref nextLinkLabelBoundary),
                        '<' => ReadAutoLink(text, index),
                        _ => default,
                    };
                }

                if (token.Length > 0)
                {
                    (tokens ??= new()).Add(token);
                    index += token.Length;
                }
                else
                {
                    index++;
                }
            }

            return tokens is null ? Array.Empty<CopilotMarkdownInlineToken>() : tokens;
        }

        private static CopilotMarkdownInlineToken ReadEmphasis(string text, int start, ref int nextBoundary)
        {
            var bold = start + 1 < text.Length && text[start + 1] == '*';
            var delimiterLength = bold ? 2 : 1;
            var contentStart = start + delimiterLength;
            if (contentStart >= text.Length || text[contentStart] is '*' or '\r' or '\n')
                return default;

            var closingStart = FindNextBoundary(text, contentStart, '*', ref nextBoundary);
            if (closingStart == text.Length || text[closingStart] != '*'
                || (bold && (closingStart + 1 == text.Length || text[closingStart + 1] != '*')))
            {
                return default;
            }

            return new(bold ? CopilotMarkdownInlineKind.Bold : CopilotMarkdownInlineKind.Italic,
                start, closingStart + delimiterLength - start, contentStart, closingStart - contentStart);
        }

        private static CopilotMarkdownInlineToken ReadLink(
            string text,
            int start,
            Dictionary<int, int>? targetClosers,
            ref int nextBoundary)
        {
            if (targetClosers == null)
                return default;

            // Every '[' before the same ']' shares its target. Do not scan that suffix again
            // when a code span consumes an earlier candidate inside the label.
            var labelEnd = FindNextBoundary(text, start + 1, ']', ref nextBoundary);
            if (labelEnd <= start + 1 || labelEnd + 1 >= text.Length || text[labelEnd] != ']'
                || !targetClosers.TryGetValue(labelEnd + 1, out var targetEnd))
            {
                return default;
            }

            var length = targetEnd + 1 - start;
            return new(CopilotMarkdownInlineKind.Link, start, length, start, length);
        }

        private static CopilotMarkdownInlineToken ReadAutoLink(string text, int start)
        {
            var suffix = text.AsSpan(start + 1);
            var prefixLength = suffix.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? 8
                : suffix.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? 7 : 0;
            if (prefixLength == 0)
                return default;

            var contentStart = start + 1 + prefixLength;
            for (var end = contentStart; end < text.Length; end++)
            {
                if (text[end] == '>')
                {
                    if (end == contentStart)
                        return default;

                    var length = end + 1 - start;
                    return new(CopilotMarkdownInlineKind.Link, start, length, start, length);
                }
                if (text[end] == '<' || char.IsWhiteSpace(text[end]))
                    break;
            }
            return default;
        }

        private static int FindNextBoundary(string text, int start, char marker, ref int nextBoundary)
        {
            if (nextBoundary < start)
            {
                nextBoundary = start;
                while (nextBoundary < text.Length && text[nextBoundary] != marker
                    && text[nextBoundary] != '\r' && text[nextBoundary] != '\n')
                {
                    nextBoundary++;
                }
            }
            return nextBoundary;
        }

        private static void IndexDelimiters(
            string text,
            bool includeLinkTargets,
            out Dictionary<int, int>? codeClosers,
            out Dictionary<int, int>? linkTargetClosers)
        {
            codeClosers = null;
            linkTargetClosers = null;
            Dictionary<int, (int Start, int LineStart)>? previousCodeRuns = null;
            List<ParenthesisFrame>? parentheses = null;
            var lineStart = 0;
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (character is '\r' or '\n')
                {
                    lineStart = index + 1;
                    parentheses?.Clear();
                }
                else if (character == '`')
                {
                    var runStart = index;
                    while (index + 1 < text.Length && text[index + 1] == '`')
                        index++;
                    var length = index + 1 - runStart;
                    previousCodeRuns ??= new();
                    if (previousCodeRuns.TryGetValue(length, out var previous) && previous.LineStart == lineStart)
                        (codeClosers ??= new())[previous.Start] = runStart;
                    previousCodeRuns[length] = (runStart, lineStart);
                }

                if (!includeLinkTargets)
                    continue;

                if (character == '(')
                {
                    (parentheses ??= new()).Add(new(index, 1));
                }
                else if (character == ')' && parentheses is { Count: > 0 })
                {
                    var frame = parentheses[^1];
                    parentheses.RemoveAt(parentheses.Count - 1);
                    // The existing target grammar permits nonempty outer parentheses with
                    // at most one additional level of parentheses inside them.
                    if (frame.Depth <= 2 && index > frame.Start + 1)
                        (linkTargetClosers ??= new())[frame.Start] = index;
                    if (parentheses.Count > 0)
                    {
                        var parent = parentheses[^1];
                        parentheses[^1] = parent with { Depth = Math.Max(parent.Depth, frame.Depth + 1) };
                    }
                }
            }
        }

        private readonly record struct ParenthesisFrame(int Start, int Depth);
    }
}
