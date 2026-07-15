using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    internal enum CopilotMarkdownTableAlignment
    {
        Default,
        Left,
        Center,
        Right,
    }

    internal sealed class CopilotMarkdownTableModel
    {
        public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();

        public IReadOnlyList<CopilotMarkdownTableAlignment> Alignments { get; init; } = Array.Empty<CopilotMarkdownTableAlignment>();

        public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = Array.Empty<IReadOnlyList<string>>();

        public bool WasTruncated { get; init; }
    }

    internal static class CopilotMarkdownTableParser
    {
        internal const int MaximumColumns = 12;
        internal const int MaximumRows = 512;
        private static readonly Regex SeparatorCellRegex = new(@"^:?-{3,}:?$", RegexOptions.Compiled);

        public static bool TryParse(
            IReadOnlyList<string> lines,
            int startIndex,
            out CopilotMarkdownTableModel table,
            out int consumedLineCount)
        {
            table = new CopilotMarkdownTableModel();
            consumedLineCount = 0;
            if (lines == null || startIndex < 0 || startIndex + 1 >= lines.Count)
                return false;

            if (!TrySplitRow(lines[startIndex], out var headers)
                || headers.Count < 2
                || headers.Count > MaximumColumns
                || !TrySplitRow(lines[startIndex + 1], out var separatorCells)
                || separatorCells.Count != headers.Count
                || separatorCells.Any(cell => !SeparatorCellRegex.IsMatch(cell.Trim())))
            {
                return false;
            }

            var alignments = separatorCells.Select(ParseAlignment).ToArray();
            var rows = new List<IReadOnlyList<string>>();
            var sourceRowCount = 0;
            var index = startIndex + 2;
            while (index < lines.Count
                && !string.IsNullOrWhiteSpace(lines[index])
                && TrySplitRow(lines[index], out var cells))
            {
                sourceRowCount++;
                if (rows.Count < MaximumRows)
                    rows.Add(NormalizeCells(cells, headers.Count));
                index++;
            }

            consumedLineCount = index - startIndex;
            table = new CopilotMarkdownTableModel
            {
                Headers = headers,
                Alignments = alignments,
                Rows = rows,
                WasTruncated = sourceRowCount > MaximumRows,
            };
            return true;
        }

        private static CopilotMarkdownTableAlignment ParseAlignment(string separatorCell)
        {
            var trimmed = separatorCell.Trim();
            if (trimmed.StartsWith(':') && trimmed.EndsWith(':'))
                return CopilotMarkdownTableAlignment.Center;
            if (trimmed.EndsWith(':'))
                return CopilotMarkdownTableAlignment.Right;
            if (trimmed.StartsWith(':'))
                return CopilotMarkdownTableAlignment.Left;
            return CopilotMarkdownTableAlignment.Default;
        }

        private static string[] NormalizeCells(IReadOnlyList<string> cells, int columnCount)
        {
            var normalized = new string[columnCount];
            for (var index = 0; index < columnCount; index++)
                normalized[index] = index < cells.Count ? cells[index] : string.Empty;
            return normalized;
        }

        private static bool TrySplitRow(string source, out IReadOnlyList<string> cells)
        {
            cells = Array.Empty<string>();
            var trimmed = (source ?? string.Empty).Trim();
            if (!trimmed.Contains('|'))
                return false;

            var values = new List<string>();
            var builder = new StringBuilder();
            var inCodeSpan = false;
            for (var index = 0; index < trimmed.Length; index++)
            {
                var character = trimmed[index];
                if (character == '\\' && index + 1 < trimmed.Length && trimmed[index + 1] == '|')
                {
                    builder.Append('|');
                    index++;
                    continue;
                }

                if (character == '`')
                {
                    inCodeSpan = !inCodeSpan;
                    builder.Append(character);
                    continue;
                }

                if (character == '|' && !inCodeSpan)
                {
                    values.Add(builder.ToString().Trim());
                    builder.Clear();
                    continue;
                }

                builder.Append(character);
            }
            values.Add(builder.ToString().Trim());

            if (trimmed.StartsWith('|') && values.Count > 0)
                values.RemoveAt(0);
            if (trimmed.EndsWith('|') && values.Count > 0)
                values.RemoveAt(values.Count - 1);
            if (values.Count < 2)
                return false;

            cells = values;
            return true;
        }
    }
}
