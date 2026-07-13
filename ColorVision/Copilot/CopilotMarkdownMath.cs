using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public readonly record struct CopilotMarkdownInlineSegment(
        string Content,
        bool IsMath,
        string OpeningDelimiter = "",
        string ClosingDelimiter = "")
    {
        public string OriginalText => IsMath ? OpeningDelimiter + Content + ClosingDelimiter : Content;
    }

    public readonly record struct CopilotMarkdownDisplayFormula(
        string Content,
        string OpeningDelimiter,
        string ClosingDelimiter)
    {
        public string OriginalText => OpeningDelimiter + Content + ClosingDelimiter;
    }

    public static class CopilotMarkdownMath
    {
        public static IReadOnlyList<CopilotMarkdownInlineSegment> ParseInline(string? text)
        {
            var source = text ?? string.Empty;
            if (source.Length == 0)
                return Array.Empty<CopilotMarkdownInlineSegment>();

            var segments = new List<CopilotMarkdownInlineSegment>();
            var textStart = 0;
            var inCode = false;
            var index = 0;
            while (index < source.Length)
            {
                if (source[index] == '\\' && index + 1 < source.Length && source[index + 1] is '$' or '`')
                {
                    index += 2;
                    continue;
                }

                if (source[index] == '`')
                {
                    inCode = !inCode;
                    index++;
                    continue;
                }

                if (!inCode && TryFindInlineMath(source, index, out var contentStart, out var contentLength, out var endIndex, out var opening, out var closing))
                {
                    AddText(segments, source, textStart, index - textStart);
                    segments.Add(new CopilotMarkdownInlineSegment(source.Substring(contentStart, contentLength), true, opening, closing));
                    index = endIndex;
                    textStart = index;
                    continue;
                }

                index++;
            }

            AddText(segments, source, textStart, source.Length - textStart);
            return segments;
        }

        public static bool TryParseDisplayLine(string? line, out IReadOnlyList<CopilotMarkdownDisplayFormula> formulas)
        {
            var source = line?.Trim() ?? string.Empty;
            var parsed = new List<CopilotMarkdownDisplayFormula>();
            var index = 0;
            while (index < source.Length)
            {
                SkipWhitespace(source, ref index);
                if (index >= source.Length)
                    break;

                string openingDelimiter;
                string closingDelimiter;
                if (source.AsSpan(index).StartsWith("$$", StringComparison.Ordinal))
                {
                    index += 2;
                    openingDelimiter = "$$";
                    closingDelimiter = "$$";
                }
                else if (source.AsSpan(index).StartsWith("\\[", StringComparison.Ordinal))
                {
                    index += 2;
                    openingDelimiter = "\\[";
                    closingDelimiter = "\\]";
                }
                else
                {
                    formulas = Array.Empty<CopilotMarkdownDisplayFormula>();
                    return false;
                }

                var closingIndex = source.IndexOf(closingDelimiter, index, StringComparison.Ordinal);
                if (closingIndex < 0 || string.IsNullOrWhiteSpace(source[index..closingIndex]))
                {
                    formulas = Array.Empty<CopilotMarkdownDisplayFormula>();
                    return false;
                }

                parsed.Add(new CopilotMarkdownDisplayFormula(
                    source[index..closingIndex].Trim(),
                    openingDelimiter,
                    closingDelimiter));
                index = closingIndex + closingDelimiter.Length;
            }

            formulas = parsed;
            return parsed.Count > 0;
        }

        public static bool TryStartDisplayBlock(
            string? line,
            out string openingDelimiter,
            out string closingDelimiter,
            out string initialContent)
        {
            var source = line?.Trim() ?? string.Empty;
            if (source.StartsWith("$$", StringComparison.Ordinal)
                && source.IndexOf("$$", 2, StringComparison.Ordinal) < 0)
            {
                openingDelimiter = "$$";
                closingDelimiter = "$$";
                initialContent = source[2..];
                return true;
            }

            if (source.StartsWith("\\[", StringComparison.Ordinal)
                && source.IndexOf("\\]", 2, StringComparison.Ordinal) < 0)
            {
                openingDelimiter = "\\[";
                closingDelimiter = "\\]";
                initialContent = source[2..];
                return true;
            }

            openingDelimiter = string.Empty;
            closingDelimiter = string.Empty;
            initialContent = string.Empty;
            return false;
        }

        private static bool TryFindInlineMath(
            string source,
            int index,
            out int contentStart,
            out int contentLength,
            out int endIndex,
            out string openingDelimiter,
            out string closingDelimiter)
        {
            contentStart = 0;
            contentLength = 0;
            endIndex = index;
            openingDelimiter = string.Empty;
            closingDelimiter = string.Empty;

            if (source.AsSpan(index).StartsWith("\\(", StringComparison.Ordinal))
            {
                var closingIndex = source.IndexOf("\\)", index + 2, StringComparison.Ordinal);
                if (closingIndex < 0 || string.IsNullOrWhiteSpace(source[(index + 2)..closingIndex]))
                    return false;

                contentStart = index + 2;
                contentLength = closingIndex - contentStart;
                endIndex = closingIndex + 2;
                openingDelimiter = "\\(";
                closingDelimiter = "\\)";
                return true;
            }

            if (source[index] != '$'
                || index + 1 >= source.Length
                || source[index + 1] == '$'
                || char.IsWhiteSpace(source[index + 1]))
            {
                return false;
            }

            for (var closingIndex = index + 1; closingIndex < source.Length; closingIndex++)
            {
                if (source[closingIndex] != '$'
                    || IsEscaped(source, closingIndex)
                    || (closingIndex + 1 < source.Length && source[closingIndex + 1] == '$')
                    || char.IsWhiteSpace(source[closingIndex - 1]))
                {
                    continue;
                }

                contentStart = index + 1;
                contentLength = closingIndex - contentStart;
                endIndex = closingIndex + 1;
                openingDelimiter = "$";
                closingDelimiter = "$";
                return contentLength > 0;
            }

            return false;
        }

        private static bool IsEscaped(string source, int index)
        {
            var slashCount = 0;
            for (var position = index - 1; position >= 0 && source[position] == '\\'; position--)
                slashCount++;
            return slashCount % 2 == 1;
        }

        private static void AddText(List<CopilotMarkdownInlineSegment> segments, string source, int start, int length)
        {
            if (length > 0)
                segments.Add(new CopilotMarkdownInlineSegment(source.Substring(start, length), false));
        }

        private static void SkipWhitespace(string source, ref int index)
        {
            while (index < source.Length && char.IsWhiteSpace(source[index]))
                index++;
        }
    }
}
