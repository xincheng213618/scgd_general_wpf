using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotMarkdownView
    {
        private sealed record MarkdownCodeSelection(
            int BlockIndex, int Start, string Text, double HorizontalOffset, double VerticalOffset, TextBox? FocusedEditor);

        private static IEnumerable<TextBox> GetCodeEditors(FlowDocument document) => document.Blocks
            .OfType<BlockUIContainer>().Select(block => block.Tag).OfType<TextBox>();

        private IReadOnlyList<MarkdownCodeSelection> CaptureCodeSelections(string markdown)
        {
            if (_renderedMarkdown == null || !markdown.StartsWith(_renderedMarkdown, StringComparison.Ordinal))
                return Array.Empty<MarkdownCodeSelection>();

            var selections = new List<MarkdownCodeSelection>();
            var index = 0;
            foreach (var editor in GetCodeEditors(DocumentViewer.Document))
            {
                if (editor.SelectionLength > 0 || editor.IsKeyboardFocused
                    || editor.HorizontalOffset > 0 || editor.VerticalOffset > 0)
                {
                    selections.Add(new MarkdownCodeSelection(index, editor.SelectionStart, editor.SelectedText,
                        editor.HorizontalOffset, editor.VerticalOffset, editor.IsKeyboardFocused ? editor : null));
                }
                index++;
            }
            return selections;
        }

        private void RestoreCodeSelections(IReadOnlyList<MarkdownCodeSelection> selections, string markdown)
        {
            if (selections.Count == 0)
                return;

            var document = DocumentViewer.Document;
            var selectionRevision = _selectionRevision;
            var selectionDataContext = DataContext;
            var editors = GetCodeEditors(document).ToArray();
            foreach (var selection in selections)
            {
                if (selection.BlockIndex >= editors.Length)
                    continue;

                var editor = editors[selection.BlockIndex];
                if (editor.SelectionLength > 0 || selection.Start > editor.Text.Length)
                    continue;

                // Closing a fence removes its preceding line break from the code
                // editor. Retain the selected code when only that CR/LF tail vanished.
                var length = Math.Min(selection.Text.Length, editor.Text.Length - selection.Start);
                if (!editor.Text.AsSpan(selection.Start, length).SequenceEqual(selection.Text.AsSpan(0, length))
                    || (length < selection.Text.Length && selection.Text.TrimEnd('\r', '\n').Length > length))
                {
                    continue;
                }

                editor.Select(selection.Start, length);

                // The new document's embedded controls acquire their visual parents
                // during layout. Until Loaded, WPF still remembers the old editor as
                // its keyboard target. Hand over only if that exact target remains;
                // a click into the composer or any other control cancels the handover.
                void RestoreViewportAndFocus(object? sender, RoutedEventArgs e)
                {
                    editor.Loaded -= RestoreViewportAndFocus;
                    if (!IsLoaded || !ReferenceEquals(DocumentViewer.Document, document)
                        || selectionRevision != _selectionRevision || !ReferenceEquals(DataContext, selectionDataContext)
                        || !(Markdown ?? string.Empty).StartsWith(markdown, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (selection.FocusedEditor != null
                        && ReferenceEquals(Keyboard.FocusedElement, selection.FocusedEditor)
                        && Window.GetWindow(this)?.IsActive == true)
                    {
                        editor.Focus();
                    }
                    editor.ScrollToHorizontalOffset(selection.HorizontalOffset);
                    editor.ScrollToVerticalOffset(selection.VerticalOffset);
                }

                if (editor.IsLoaded)
                    RestoreViewportAndFocus(editor, new RoutedEventArgs());
                else
                    editor.Loaded += RestoreViewportAndFocus;
            }
        }
    }
}
