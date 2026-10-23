using System;
using System.Windows.Documents;

namespace ColorVision.Copilot
{
    public partial class CopilotMarkdownView
    {
        private sealed record MarkdownTextSelection(int StartSymbol, int EndSymbol, int Start, int End, string Text, bool IsReversed);

        private MarkdownTextSelection? CaptureTextSelection(string markdown)
        {
            // A recycled view can represent another message. Only retain a selection
            // while appending to the same response or laying out its unchanged text.
            if (_renderedMarkdown == null
                || !markdown.StartsWith(_renderedMarkdown, StringComparison.Ordinal)
                || DocumentViewer.Selection.IsEmpty)
            {
                return null;
            }

            var selection = DocumentViewer.Selection;
            var start = selection.Start.GetInsertionPosition(LogicalDirection.Forward);
            var end = selection.End.GetInsertionPosition(LogicalDirection.Backward);
            if (start == null || end == null || start.CompareTo(end) >= 0)
                return null;

            var text = new TextRange(start, end).Text;
            if (text.Length == 0)
                return null;

            var documentStart = DocumentViewer.Document.ContentStart;
            return new MarkdownTextSelection(
                documentStart.GetOffsetToPosition(start),
                documentStart.GetOffsetToPosition(end),
                new TextRange(documentStart, start).Text.Length,
                new TextRange(documentStart, end).Text.Length,
                text,
                DocumentViewer.CaretPosition.CompareTo(selection.Start) == 0);
        }

        private void RestoreTextSelection(MarkdownTextSelection? selection)
        {
            if (selection == null || !DocumentViewer.Selection.IsEmpty)
                return;

            var document = DocumentViewer.Document;
            // Stable markup retains its native positions, including table cells.
            // A TextRange beginning outside a table expands partial cells to rows,
            // so its text length cannot identify positions inside those cells.
            var start = document.ContentStart.GetPositionAtOffset(selection.StartSymbol);
            var end = document.ContentStart.GetPositionAtOffset(selection.EndSymbol);
            if (start == null || end == null
                || !string.Equals(new TextRange(start, end).Text, selection.Text, StringComparison.Ordinal))
            {
                start = FindTextPosition(document, selection.Start);
                end = FindTextPosition(document, selection.End);
            }
            if (start == null || end == null
                || !string.Equals(new TextRange(start, end).Text, selection.Text, StringComparison.Ordinal))
            {
                // Completing a Markdown delimiter can change the rendered prefix.
                // Relocate only an unambiguous match; never select different text.
                var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
                var index = text.IndexOf(selection.Text, StringComparison.Ordinal);
                if (index < 0 || text.IndexOf(selection.Text, index + 1, StringComparison.Ordinal) >= 0)
                    return;

                start = FindTextPosition(document, index);
                end = FindTextPosition(document, index + selection.Text.Length);
                if (start == null || end == null
                    || !string.Equals(new TextRange(start, end).Text, selection.Text, StringComparison.Ordinal))
                {
                    return;
                }
            }

            DocumentViewer.Selection.Select(selection.IsReversed ? end : start, selection.IsReversed ? start : end);
        }

        private static TextPointer? FindTextPosition(FlowDocument document, int textOffset)
        {
            // TextPointer offsets count formatting elements as well as characters.
            // Search the rendered text length when inline formatting changes.
            var start = document.ContentStart;
            var low = 0;
            var high = start.GetOffsetToPosition(document.ContentEnd);
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                var position = start.GetPositionAtOffset(middle)!;
                if (new TextRange(start, position).Text.Length < textOffset)
                    low = middle + 1;
                else
                    high = middle;
            }

            var result = start.GetPositionAtOffset(low)?.GetInsertionPosition(LogicalDirection.Forward);
            return result != null && new TextRange(start, result).Text.Length == textOffset ? result : null;
        }
    }
}
