#pragma warning disable CA1822
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColorVision.Copilot
{
    public partial class CopilotMarkdownView
    {
        private void AddTable(CopilotMarkdownTableModel model)
        {
            var availableWidth = GetAvailableTableWidth();
            if (ShouldUseKeyValueLayout(model, availableWidth))
            {
                AddTableAsKeyValueRecords(model);
                return;
            }

            var table = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 3, 0, 10),
            };
            table.SetResourceReference(TextElement.ForegroundProperty, "GlobalTextBrush");
            var columnWidths = CalculateTableColumnWidths(model, availableWidth);
            for (var columnIndex = 0; columnIndex < model.Headers.Count; columnIndex++)
            {
                table.Columns.Add(new TableColumn { Width = new GridLength(columnWidths[columnIndex]) });
            }

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(CreateTableRow(model.Headers, model.Alignments, isHeader: true, isSection: false));
            foreach (var cells in model.Rows)
            {
                var isSection = !string.IsNullOrWhiteSpace(cells[0]) && cells.Skip(1).All(string.IsNullOrWhiteSpace);
                rowGroup.Rows.Add(CreateTableRow(cells, model.Alignments, isHeader: false, isSection));
            }
            if (model.WasTruncated)
            {
                var truncatedCells = Enumerable.Repeat(string.Empty, model.Headers.Count).ToArray();
                truncatedCells[0] = $"… table limited to {CopilotMarkdownTableParser.MaximumRows} rows";
                rowGroup.Rows.Add(CreateTableRow(truncatedCells, model.Alignments, isHeader: false, isSection: true));
            }

            table.RowGroups.Add(rowGroup);
            CurrentDocument.Blocks.Add(table);
        }

        private double GetAvailableTableWidth()
        {
            var width = DocumentViewer.ActualWidth;
            if (!double.IsFinite(width) || width < 1)
                width = ActualWidth;
            if (!double.IsFinite(width) || width < 1)
                width = 640;
            return Math.Max(160, width - 4);
        }

        private static bool ShouldUseKeyValueLayout(CopilotMarkdownTableModel model, double availableWidth)
        {
            if (model.Headers.Count < 2)
                return false;

            var hasLongValue = model.Rows.Take(64).SelectMany(row => row.Skip(1)).Any(value => EstimateTableCellWidth(value) >= 220);
            return model.Headers.Count == 2
                ? availableWidth < 320 && hasLongValue
                : availableWidth < model.Headers.Count * 140 && hasLongValue;
        }

        private static double[] CalculateTableColumnWidths(CopilotMarkdownTableModel model, double availableWidth)
        {
            var columnCount = model.Headers.Count;
            var minimumWidths = new double[columnCount];
            var preferredWidths = new double[columnCount];
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var isFirst = columnIndex == 0;
                var isCompactLast = columnCount > 2 && columnIndex == columnCount - 1;
                var minimumWidth = isFirst ? 96 : isCompactLast ? 72 : 88;
                var maximumWidth = isFirst ? 220 : isCompactLast ? 140 : 420;
                minimumWidths[columnIndex] = minimumWidth;
                preferredWidths[columnIndex] = MeasureBoundedTableColumn(model, columnIndex, minimumWidth, maximumWidth);
            }

            var minimumTotal = minimumWidths.Sum();
            if (minimumTotal >= availableWidth)
            {
                var scale = availableWidth / minimumTotal;
                return minimumWidths.Select(width => width * scale).ToArray();
            }

            var preferredTotal = preferredWidths.Sum();
            if (preferredTotal <= availableWidth)
            {
                var result = preferredWidths.ToArray();
                var flexibleColumns = Enumerable.Range(1, Math.Max(0, columnCount - 1))
                    .Where(index => columnCount <= 2 || index < columnCount - 1)
                    .ToArray();
                if (flexibleColumns.Length == 0)
                    flexibleColumns = [columnCount - 1];

                var extraPerColumn = (availableWidth - preferredTotal) / flexibleColumns.Length;
                foreach (var columnIndex in flexibleColumns)
                    result[columnIndex] += extraPerColumn;
                return result;
            }

            var remainingWidth = availableWidth - minimumTotal;
            var growthTotal = preferredWidths.Select((width, index) => width - minimumWidths[index]).Sum();
            if (growthTotal <= 0)
                return minimumWidths;

            return minimumWidths
                .Select((width, index) => width + remainingWidth * (preferredWidths[index] - width) / growthTotal)
                .ToArray();
        }

        private void AddTableAsKeyValueRecords(CopilotMarkdownTableModel model)
        {
            foreach (var row in model.Rows)
            {
                var section = new Section
                {
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 2, 0, 6),
                    Padding = new Thickness(0, 0, 0, 6),
                };
                section.SetResourceReference(Block.BorderBrushProperty, "ButtonBorderBrush");

                if (model.Headers.Count == 2 && !string.IsNullOrWhiteSpace(row[0]))
                {
                    AddKeyValuePair(section, row[0], row[1]);
                }
                else
                {
                    for (var columnIndex = 0; columnIndex < model.Headers.Count; columnIndex++)
                    {
                        if (!string.IsNullOrWhiteSpace(row[columnIndex]))
                            AddKeyValuePair(section, model.Headers[columnIndex], row[columnIndex]);
                    }
                }

                if (section.Blocks.Count > 0)
                    CurrentDocument.Blocks.Add(section);
            }

            if (model.WasTruncated)
                AddTextBlock($"… table limited to {CopilotMarkdownTableParser.MaximumRows} rows", new Thickness(0, 0, 0, 8));
        }

        private static void AddKeyValuePair(Section section, string key, string value)
        {
            var paragraph = CreateParagraph(12.5, FontWeights.Normal, new Thickness(0, 0, 0, 4));
            var keyRun = new Run(key) { FontWeight = FontWeights.SemiBold };
            paragraph.Inlines.Add(keyRun);
            paragraph.Inlines.Add(new LineBreak());
            AddInlines(paragraph.Inlines, value);
            section.Blocks.Add(paragraph);
        }

        private static double MeasureBoundedTableColumn(
            CopilotMarkdownTableModel model,
            int columnIndex,
            double minimumWidth,
            double maximumWidth)
        {
            var values = model.Rows
                .Take(64)
                .Select(row => row[columnIndex])
                .Prepend(model.Headers[columnIndex]);
            var contentWidth = values.Max(EstimateTableCellWidth) + 28;
            return Math.Clamp(contentWidth, minimumWidth, maximumWidth);
        }

        private static double EstimateTableCellWidth(string? value)
        {
            var width = 0d;
            foreach (var character in value ?? string.Empty)
            {
                width += character <= 0x7f ? 7 : 13;
                if (width >= 220)
                    return width;
            }
            return width;
        }

        private static TableRow CreateTableRow(
            IReadOnlyList<string> cells,
            IReadOnlyList<CopilotMarkdownTableAlignment> alignments,
            bool isHeader,
            bool isSection)
        {
            var row = new TableRow();
            for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
            {
                var paragraph = CreateParagraph(12.5, isHeader || isSection ? FontWeights.SemiBold : FontWeights.Normal, new Thickness(0));
                paragraph.LineHeight = 18;
                paragraph.TextAlignment = alignments[columnIndex] switch
                {
                    CopilotMarkdownTableAlignment.Center => TextAlignment.Center,
                    CopilotMarkdownTableAlignment.Right => TextAlignment.Right,
                    _ => TextAlignment.Left,
                };
                AddInlines(paragraph.Inlines, cells[columnIndex]);

                var cell = new TableCell(paragraph)
                {
                    BorderThickness = new Thickness(columnIndex == 0 ? 1 : 0, isHeader ? 1 : 0, 1, 1),
                    Padding = new Thickness(7, isHeader || isSection ? 5 : 4, 7, isHeader || isSection ? 5 : 4),
                };
                cell.SetResourceReference(Block.BorderBrushProperty, "ButtonBorderBrush");
                if (isHeader || isSection)
                    cell.SetResourceReference(Block.BackgroundProperty, "GlobalBorderBrush1");
                row.Cells.Add(cell);
            }
            return row;
        }
    }
}
