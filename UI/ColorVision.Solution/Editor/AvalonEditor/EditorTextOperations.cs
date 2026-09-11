using ICSharpCode.AvalonEdit.Document;
using AvalonTextEditor = ICSharpCode.AvalonEdit.TextEditor;

namespace ColorVision.Solution.Editor.AvalonEditor;

internal static class EditorTextOperations
{
    internal static string? CommentPrefix(string? language) => language switch
    {
        "Python" or "PowerShell" or "Boo" or "Ruby" or "YAML" => "#",
        "C#" or "C++" or "Java" or "JavaScript" or "PHP" => "//",
        "SQL" or "TSQL" => "--",
        _ => null
    };

    internal static (DocumentLine First, DocumentLine Last) SelectedLines(AvalonTextEditor editor)
    {
        var first = editor.Document.GetLineByOffset(editor.SelectionStart);
        int end = editor.SelectionStart + editor.SelectionLength;
        var last = editor.Document.GetLineByOffset(end);
        if (editor.SelectionLength > 0 && last.Offset == end && last.PreviousLine != null) last = last.PreviousLine;
        return (first, last);
    }

    internal static void ToggleComment(AvalonTextEditor editor, string prefix)
    {
        if (editor.IsReadOnly) return;
        var (first, last) = SelectedLines(editor);
        int firstNumber = first.LineNumber, lastNumber = last.LineNumber;
        var lines = Enumerable.Range(firstNumber, lastNumber - firstNumber + 1).Select(editor.Document.GetLineByNumber)
            .Select(line => (Line: line, Text: editor.Document.GetText(line))).Where(x => !string.IsNullOrWhiteSpace(x.Text)).ToArray();
        if (lines.Length == 0) return;
        bool uncomment = lines.All(x => x.Text.TrimStart().StartsWith(prefix, StringComparison.Ordinal));
        using (editor.Document.RunUpdate())
        {
            foreach (var (line, text) in lines.Reverse())
            {
                int indent = text.Length - text.TrimStart().Length;
                if (uncomment)
                {
                    int length = prefix.Length;
                    if (text.Length > indent + length && text[indent + length] == ' ') length++;
                    editor.Document.Remove(line.Offset + indent, length);
                }
                else editor.Document.Insert(line.Offset + indent, prefix + " ");
            }
        }
        var startLine = editor.Document.GetLineByNumber(firstNumber);
        var endLine = editor.Document.GetLineByNumber(lastNumber);
        editor.Select(startLine.Offset, endLine.EndOffset - startLine.Offset);
    }

    internal static void Duplicate(AvalonTextEditor editor)
    {
        if (editor.IsReadOnly) return;
        if (editor.SelectionLength > 0)
        {
            string text = editor.SelectedText;
            int end = editor.SelectionStart + editor.SelectionLength;
            editor.Document.Insert(end, text);
            editor.Select(end, text.Length);
        }
        else
        {
            var line = editor.Document.GetLineByOffset(editor.CaretOffset);
            string newline = NewLine(editor.Document);
            int column = editor.CaretOffset - line.Offset;
            string text = editor.Document.GetText(line);
            int offset = line.EndOffset;
            editor.Document.Insert(offset, newline + text);
            editor.CaretOffset = offset + newline.Length + column;
        }
        editor.TextArea.Caret.BringCaretToView();
    }

    internal static void MoveLines(AvalonTextEditor editor, int direction)
    {
        if (editor.IsReadOnly) return;
        var (first, last) = SelectedLines(editor);
        var neighbor = direction < 0 ? first.PreviousLine : last.NextLine;
        if (neighbor == null) return;
        int caretColumn = editor.TextArea.Caret.Column;
        bool selected = editor.SelectionLength > 0;
        int firstNumber = first.LineNumber;
        int count = last.LineNumber - firstNumber + 1;
        var rangeFirst = direction < 0 ? neighbor : first;
        var rangeLast = direction < 0 ? last : neighbor;
        var contents = Enumerable.Range(rangeFirst.LineNumber, count + 1).Select(editor.Document.GetLineByNumber).Select(l => editor.Document.GetText(l)).ToList();
        // Preserve each destination line's delimiter, including a final line without a newline.
        var delimiters = Enumerable.Range(rangeFirst.LineNumber, count + 1).Select(editor.Document.GetLineByNumber)
            .Select(l => editor.Document.GetText(l.EndOffset, l.DelimiterLength)).ToArray();
        if (direction < 0) { string value = contents[0]; contents.RemoveAt(0); contents.Add(value); }
        else { string value = contents[^1]; contents.RemoveAt(contents.Count - 1); contents.Insert(0, value); }
        string replacement = string.Concat(contents.Select((value, index) => value + delimiters[index]));
        int offset = rangeFirst.Offset;
        editor.Document.Replace(offset, rangeLast.Offset + rangeLast.TotalLength - offset, replacement);
        var newFirst = editor.Document.GetLineByNumber(firstNumber + direction);
        var newLast = editor.Document.GetLineByNumber(firstNumber + direction + count - 1);
        if (selected) editor.Select(newFirst.Offset, newLast.EndOffset - newFirst.Offset);
        else editor.CaretOffset = newFirst.Offset + Math.Min(caretColumn - 1, newFirst.Length);
        editor.TextArea.Caret.BringCaretToView();
    }

    internal static string NewLine(TextDocument document)
    {
        foreach (var line in document.Lines)
            if (line.DelimiterLength > 0) return document.GetText(line.EndOffset, line.DelimiterLength);
        return Environment.NewLine;
    }
}
