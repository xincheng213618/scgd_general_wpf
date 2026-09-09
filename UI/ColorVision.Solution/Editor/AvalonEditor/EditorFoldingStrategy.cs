using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Indentation;

namespace ColorVision.Solution.Editor.AvalonEditor;

internal static class EditorFoldingStrategy
{
    internal static bool Supports(string? language) => language is "Python" or "C#" or "C++" or "Java" or "JavaScript" or "PHP" or "Json";

    internal static IReadOnlyList<NewFolding> CreateFoldings(TextDocument document, IHighlightingDefinition definition, int tabSize, EditorCodeAnalysis? analysis = null, CancellationToken cancellationToken = default)
    {
        using var highlighter = new DocumentHighlighter(document, definition);
        var result = new List<NewFolding>();
        var braces = new Stack<(char Brace, int Offset, int Line)>();
        var blocks = new Stack<(int Indent, int Offset)>();
        bool python = definition.Name == "Python";
        int lastCodeEnd = 0, logicalIndent = 0, logicalStart = 0;
        foreach (var line in document.Lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string original = document.GetText(line);
            var highlighted = highlighter.HighlightLine(line.LineNumber);
            string code = GetCodeLine(original, line.Offset, highlighted);
            analysis?.ReadLine(line, code);
            if (string.IsNullOrWhiteSpace(code))
            {
                if (python && !string.IsNullOrWhiteSpace(original) && highlighted.Sections.Any(s =>
                    ThemeAwareHighlightingColorizer.GetForegroundBrushResourceKey(s.Color) == "EditorSyntaxStringBrush")) lastCodeEnd = line.EndOffset;
                continue;
            }
            int whitespace = 0, indent = 0;
            while (whitespace < original.Length && original[whitespace] is ' ' or '\t')
            {
                indent += original[whitespace++] == '\t' ? tabSize - indent % tabSize : 1;
            }
            if (python && braces.Count == 0)
            {
                while (blocks.TryPeek(out var block) && indent <= block.Indent)
                {
                    blocks.Pop();
                    if (lastCodeEnd > block.Offset) result.Add(new NewFolding(block.Offset, lastCodeEnd));
                }
                logicalIndent = indent;
                logicalStart = line.EndOffset;
            }
            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                if (c is '{' or '[' || python && c == '(') braces.Push((c, line.Offset + i, line.LineNumber));
                else if (c is '}' or ']' || python && c == ')')
                {
                    char open = c switch { '}' => '{', ']' => '[', _ => '(' };
                    if (braces.TryPeek(out var start) && start.Brace == open)
                    {
                        braces.Pop();
                        if (start.Line < line.LineNumber && !python) result.Add(new NewFolding(start.Offset + 1, line.Offset + i));
                    }
                }
            }
            if (python && braces.Count == 0 && code.TrimEnd().EndsWith(':')) blocks.Push((logicalIndent, logicalStart));
            lastCodeEnd = line.EndOffset;
        }
        while (blocks.TryPop(out var block))
            if (lastCodeEnd > block.Offset) result.Add(new NewFolding(block.Offset, lastCodeEnd));
        return result.OrderBy(f => f.StartOffset).ToArray();
    }

    internal static string GetCodeLine(string text, int offset, HighlightedLine highlighted)
    {
        char[] code = text.ToCharArray();
        foreach (var section in highlighted.Sections)
        {
            string? role = ThemeAwareHighlightingColorizer.GetForegroundBrushResourceKey(section.Color);
            if (role is not ("EditorSyntaxStringBrush" or "EditorSyntaxCommentBrush")) continue;
            int start = Math.Clamp(section.Offset - offset, 0, code.Length);
            int end = Math.Clamp(section.Offset + section.Length - offset, start, code.Length);
            Array.Fill(code, ' ', start, end - start);
        }
        return new string(code);
    }
}

internal sealed class PythonIndentationStrategy : DefaultIndentationStrategy
{
    private readonly Func<string> _indent;
    private readonly Func<IHighlighter?> _getHighlighter;
    public PythonIndentationStrategy(Func<string> indent, Func<IHighlighter?> getHighlighter)
    {
        _indent = indent;
        _getHighlighter = getHighlighter;
    }
    public override void IndentLine(TextDocument document, DocumentLine line)
    {
        base.IndentLine(document, line);
        if (line.PreviousLine == null) return;
        var highlighter = _getHighlighter();
        string previous = document.GetText(line.PreviousLine);
        if (highlighter != null) previous = EditorFoldingStrategy.GetCodeLine(previous, line.PreviousLine.Offset, highlighter.HighlightLine(line.PreviousLine.LineNumber));
        if (previous.TrimEnd().EndsWith(':')) document.Insert(line.Offset, _indent());
    }
}
