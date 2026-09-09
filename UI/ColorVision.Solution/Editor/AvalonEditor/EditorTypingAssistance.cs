using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Snippets;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AvalonTextEditor = ICSharpCode.AvalonEdit.TextEditor;

namespace ColorVision.Solution.Editor.AvalonEditor;

internal sealed class EditorTypingAssistance : IDisposable, IBackgroundRenderer
{
    private readonly AvalonTextEditor _editor;
    private readonly EditorPreferences _preferences;
    private readonly Func<string?> _language;
    private readonly List<(TextAnchor Open, TextAnchor Close, char Opening, char Closing)> _pairs = [];
    private CompletionWindow? _completion;
    private EditorCodeModel _model = EditorCodeModel.Empty;
    private ITextSourceVersion? _version;
    internal EditorCodeModel Model => ReferenceEquals(_version, _editor.Document?.Version) ? _model : EditorCodeModel.Empty;
    internal CompletionWindow? Completion => _completion;
    public KnownLayer Layer => KnownLayer.Selection;

    internal EditorTypingAssistance(AvalonTextEditor editor, EditorPreferences preferences, Func<string?> language)
    {
        _editor = editor;
        _preferences = preferences;
        _language = language;
        editor.TextArea.TextEntering += TextEntering;
        editor.TextArea.TextEntered += TextEntered;
        editor.TextArea.PreviewKeyDown += PreviewKeyDown;
        editor.TextArea.Caret.PositionChanged += CaretChanged;
        editor.TextArea.TextView.BackgroundRenderers.Add(this);
        editor.TextChanged += DocumentChanged;
        editor.IsVisibleChanged += VisibilityChanged;
        editor.Unloaded += Unloaded;
    }

    internal void SetModel(EditorCodeModel model)
    {
        _model = model;
        _version = _editor.Document.Version;
        _editor.TextArea.TextView.InvalidateLayer(Layer);
    }

    internal void Reset()
    {
        CloseCompletion();
        _pairs.Clear();
        SetModel(EditorCodeModel.Empty);
    }

    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { if (!_editor.IsVisible) CloseCompletion(); }
    private void Unloaded(object sender, RoutedEventArgs e) => CloseCompletion();
    private void DocumentChanged(object? sender, EventArgs e)
    {
        _editor.TextArea.TextView.InvalidateLayer(Layer);
        _pairs.RemoveAll(p => p.Open.IsDeleted || p.Close.IsDeleted || p.Open.Offset >= _editor.Document.TextLength
            || p.Close.Offset >= _editor.Document.TextLength || _editor.Document.GetCharAt(p.Open.Offset) != p.Opening || _editor.Document.GetCharAt(p.Close.Offset) != p.Closing);
    }
    private void CaretChanged(object? sender, EventArgs e) => _editor.TextArea.TextView.InvalidateLayer(Layer);

    private void TextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (e.Handled) return;
        // Punctuation never silently chooses a completion. Tab/Enter/double-click are explicit acceptance.
        if (e.Text.Length != 1 || !EditorCodeAnalysis.IsIdentifier(e.Text[0])) CloseCompletion();
        if (HandleTextInput(e.Text)) e.Handled = true;
    }

    internal bool HandleTextInput(string text)
    {
        if (!_preferences.AutoClosePairs || _editor.IsReadOnly || text.Length != 1 || _language() == null || _editor.TextArea.Selection is RectangleSelection) return false;
        char character = text[0];
        int caret = _editor.CaretOffset;
        if (_editor.SelectionLength == 0)
        {
            if (_language() == "Python" && character is '\'' or '"' && caret >= 2
                && _editor.Document.GetText(caret - 2, 2) == new string(character, 2)
                && (caret == 2 || _editor.Document.GetCharAt(caret - 3) != character) && !IsNonCode(caret))
            {
                _editor.Document.Insert(caret, new string(character, 4));
                for (int i = 0; i < 3; i++) AddPair(caret - 2 + i, caret + 1 + i, character, character);
                _editor.CaretOffset = caret + 1;
                return true;
            }
            var pair = _pairs.LastOrDefault(p => !p.Close.IsDeleted && p.Close.Offset == caret && p.Closing == character);
            if (pair.Close != null)
            {
                _editor.CaretOffset++;
                _pairs.Remove(pair);
                return true;
            }
        }
        char close = character switch { '(' => ')', '[' => ']', '{' => '}', '\'' => '\'', '"' => '"', _ => '\0' };
        if (close == '\0') return false;
        bool selected = _editor.SelectionLength > 0;
        if (!selected)
        {
            if (IsNonCode(caret)) return false;
            if (character is '\'' or '"' && caret > 0 && EditorCodeAnalysis.IsIdentifier(_editor.Document.GetCharAt(caret - 1)))
            {
                string prefix = EditorCodeAnalysis.WordAt(_editor.Document, caret).Word.ToLowerInvariant();
                if (_language() != "Python" || prefix is not ("f" or "r" or "b" or "u" or "fr" or "rf" or "br" or "rb")) return false;
            }
            if (caret < _editor.Document.TextLength && !char.IsWhiteSpace(_editor.Document.GetCharAt(caret)) && _editor.Document.GetCharAt(caret) is not (')' or ']' or '}' or ',' or ':' or ';')) return false;
        }
        int start = _editor.SelectionStart;
        string content = _editor.SelectedText;
        _editor.Document.Replace(start, _editor.SelectionLength, character + content + close);
        AddPair(start, start + content.Length + 1, character, close);
        _editor.Select(start + 1, content.Length);
        return true;
    }

    private void AddPair(int start, int end, char opening, char closing)
    {
        var first = _editor.Document.CreateAnchor(start);
        var last = _editor.Document.CreateAnchor(end);
        first.MovementType = last.MovementType = AnchorMovementType.AfterInsertion;
        _pairs.Add((first, last, opening, closing));
    }

    internal bool HandleEditingKey(Key key)
    {
        if (_editor.IsReadOnly || _editor.SelectionLength != 0 || !_preferences.AutoClosePairs) return false;
        int caret = _editor.CaretOffset;
        if (key == Key.Back)
        {
            if (caret >= 3 && caret + 3 <= _editor.Document.TextLength && _editor.Document.GetCharAt(caret - 1) is '\'' or '"'
                && _editor.Document.GetText(caret - 3, 6) == new string(_editor.Document.GetCharAt(caret - 1), 6)
                && _pairs.Any(p => !p.Open.IsDeleted && !p.Close.IsDeleted && p.Open.Offset == caret - 3 && p.Close.Offset == caret))
            {
                _editor.Document.Remove(caret - 3, 6);
                _editor.CaretOffset = caret - 3;
                return true;
            }
            var pair = _pairs.LastOrDefault(p => !p.Open.IsDeleted && !p.Close.IsDeleted && p.Open.Offset == caret - 1 && p.Close.Offset == caret);
            if (pair.Open == null) return false;
            _editor.Document.Remove(caret - 1, 2);
            _editor.CaretOffset = caret - 1;
            return true;
        }
        if (key == Key.Enter && caret > 0 && caret < _editor.Document.TextLength && !IsNonCode(caret))
        {
            char opening = _editor.Document.GetCharAt(caret - 1), closing = _editor.Document.GetCharAt(caret);
            if (!(opening == '{' && closing == '}' || opening == '[' && closing == ']' || opening == '(' && closing == ')')) return false;
            string line = _editor.Document.GetText(_editor.Document.GetLineByOffset(caret));
            string indent = new(line.TakeWhile(c => c is ' ' or '\t').ToArray());
            string newline = EditorTextOperations.NewLine(_editor.Document);
            string first = newline + indent + _editor.Options.IndentationString;
            _editor.Document.Insert(caret, first + newline + indent);
            _editor.CaretOffset = caret + first.Length;
            _editor.TextArea.Caret.BringCaretToView();
            return true;
        }
        return false;
    }

    private void PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None) return;
        if (_completion != null && e.Key is Key.Enter or Key.Tab)
        {
            _completion.CompletionList.RequestInsertion(e);
            e.Handled = true;
        }
        else if (_completion == null && HandleEditingKey(e.Key)) e.Handled = true;
    }

    private void TextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (!_preferences.ShowCompletion || e.Text.Length != 1 || !EditorCodeAnalysis.IsIdentifier(e.Text[0]) || _completion != null) return;
        var (offset, _) = EditorCodeAnalysis.WordAt(_editor.Document, _editor.CaretOffset);
        if (_editor.CaretOffset - offset >= 2) ShowCompletion(false);
    }

    internal bool IsNonCode(int offset)
    {
        if (_editor.TextArea.GetService(typeof(IHighlighter)) is not IHighlighter highlighter) return false;
        var line = _editor.Document.GetLineByOffset(offset);
        var sections = highlighter.HighlightLine(line.LineNumber).Sections;
        foreach (var section in sections)
        {
            string? role = ThemeAwareHighlightingColorizer.GetForegroundBrushResourceKey(section.Color);
            if (role is not ("EditorSyntaxStringBrush" or "EditorSyntaxCommentBrush")) continue;
            if (offset < section.Offset || offset > section.Offset + section.Length) continue;
            if (offset == section.Offset + section.Length && role == "EditorSyntaxStringBrush" && offset > 0)
            {
                char last = _editor.Document.GetCharAt(offset - 1);
                int slashes = 0;
                for (int i = offset - 2; i >= line.Offset && _editor.Document.GetCharAt(i) == '\\'; i--) slashes++;
                if (last is '\'' or '"' && slashes % 2 == 0) continue;
            }
            return true;
        }
        return false;
    }

    internal void ShowCompletion(bool explicitRequest)
    {
        if (_editor.IsReadOnly || !_editor.IsLoaded || !_editor.IsVisible || IsNonCode(_editor.CaretOffset)) return;
        CloseCompletion();
        int caret = _editor.CaretOffset;
        var (start, word) = EditorCodeAnalysis.WordAt(_editor.Document, caret);
        string prefix = _editor.Document.GetText(start, caret - start);
        var items = EditorCompletionItem.Create(_language(), _model, prefix, explicitRequest).ToArray();
        if (items.Length == 0) return;
        var window = new CompletionWindow(_editor.TextArea) { StartOffset = start, EndOffset = start + word.Length, Width = 350, MaxHeight = 280,
            CloseWhenCaretAtBeginning = !explicitRequest, FontFamily = _editor.FontFamily, FontSize = Math.Clamp(_editor.FontSize, 12, 18) };
        window.SetResourceReference(CompletionWindow.BackgroundProperty, "EditorBackgroundBrush");
        window.SetResourceReference(CompletionWindow.ForegroundProperty, "EditorForegroundBrush");
        window.SetResourceReference(CompletionWindow.BorderBrushProperty, "EditorOverviewBorderBrush");
        window.CompletionList.SetResourceReference(CompletionList.BackgroundProperty, "EditorBackgroundBrush");
        window.CompletionList.SetResourceReference(CompletionList.ForegroundProperty, "EditorForegroundBrush");
        foreach (var item in items) window.CompletionList.CompletionData.Add(item);
        _completion = window;
        window.Closed += (_, _) => { if (ReferenceEquals(_completion, window)) _completion = null; };
        window.Show();
        window.CompletionList.SelectItem(prefix);
    }

    internal void CloseCompletion() { _completion?.Close(); _completion = null; }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid) return;
        int caret = _editor.CaretOffset;
        var model = Model;
        int bracket = model.Brackets.ContainsKey(caret) ? caret : caret - 1;
        if (_preferences.HighlightBrackets && model.Brackets.TryGetValue(bracket, out int matching))
        {
            var pen = new Pen(_editor.TryFindResource("EditorLinkBrush") as Brush ?? Brushes.DodgerBlue, 1);
            foreach (int offset in new[] { bracket, matching })
                foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = offset, Length = 1 }))
                    drawingContext.DrawRoundedRectangle(null, pen, rect, 1, 1);
        }
        if (model.Diagnostic is { } diagnostic)
        {
            int offset = Math.Clamp(diagnostic.Offset, 0, Math.Max(0, _editor.Document.TextLength - 1));
            var pen = new Pen(_editor.TryFindResource("EditorSyntaxErrorBrush") as Brush ?? Brushes.Red, 1.5);
            foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = offset, Length = 1 }))
                for (double x = rect.Left; x < rect.Right; x += 4)
                {
                    drawingContext.DrawLine(pen, new Point(x, rect.Bottom - 1), new Point(Math.Min(x + 2, rect.Right), rect.Bottom - 3));
                    drawingContext.DrawLine(pen, new Point(Math.Min(x + 2, rect.Right), rect.Bottom - 3), new Point(Math.Min(x + 4, rect.Right), rect.Bottom - 1));
                }
        }
    }

    public void Dispose()
    {
        CloseCompletion();
        _editor.TextArea.TextEntering -= TextEntering;
        _editor.TextArea.TextEntered -= TextEntered;
        _editor.TextArea.PreviewKeyDown -= PreviewKeyDown;
        _editor.TextArea.Caret.PositionChanged -= CaretChanged;
        _editor.TextArea.TextView.BackgroundRenderers.Remove(this);
        _editor.TextChanged -= DocumentChanged;
        _editor.IsVisibleChanged -= VisibilityChanged;
        _editor.Unloaded -= Unloaded;
        _pairs.Clear();
    }
}

internal sealed class EditorCompletionItem(string text, string kind, string? template = null) : ICompletionData
{
    public ImageSource? Image => null;
    public string Text => text;
    public object Content => $"{text}    {kind}";
    public object Description => template == null ? kind : System.Text.RegularExpressions.Regex.Replace(template, @"\$\{([^}]+)\}", "$1") + "\nTab 切换占位符 · Esc 结束";
    public double Priority => template != null ? 2 : kind == "文档符号" ? 1 : 0;

    internal static IEnumerable<EditorCompletionItem> Create(string? language, EditorCodeModel model, string prefix, bool explicitRequest)
    {
        string keywords = language switch
        {
            "Python" => "False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield print len range enumerate zip sorted list dict set tuple str int float bool isinstance super self",
            "C#" => "abstract as async await base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach if implicit in int interface internal is lock long namespace new null object operator out override params partial private protected public readonly record ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using var virtual void volatile while yield",
            "JavaScript" => "async await break case catch class const continue debugger default delete do else export extends false finally for from function if import in instanceof let new null of return static super switch this throw true try typeof undefined var void while yield",
            "Json" => "true false null",
            _ => ""
        };
        var items = model.Words.Select(w => new EditorCompletionItem(w, "文档词语"))
            .Concat(keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => new EditorCompletionItem(w, "语言关键字")))
            .Concat(model.Symbols.Select(s => new EditorCompletionItem(s.Name, "文档符号"))).ToList();
        if (language == "Python")
        {
            items.Add(new("def", "代码片段", "def ${name}(${args}):\n\t${pass}"));
            items.Add(new("class", "代码片段", "class ${Name}:\n\tdef __init__(self):\n\t\t${pass}"));
            items.Add(new("for", "代码片段", "for ${item} in ${items}:\n\t${pass}"));
            items.Add(new("if", "代码片段", "if ${condition}:\n\t${pass}"));
            items.Add(new("try", "代码片段", "try:\n\t${pass}\nexcept ${Exception} as error:\n\t${pass}"));
        }
        else if (language is "C#" or "JavaScript")
        {
            items.Add(new("if", "代码片段", "if (${condition})\n{\n\t${body}\n}"));
            if (language == "C#") items.Add(new("foreach", "代码片段", "foreach (var ${item} in ${items})\n{\n\t${body}\n}"));
            else items.Add(new("for", "代码片段", "for (const ${item} of ${items}) {\n\t${body}\n}"));
        }
        return items.Where(i => i.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (explicitRequest || i.Text != prefix || i.Priority == 2))
            .GroupBy(i => i.Text, StringComparer.Ordinal).Select(g => g.OrderByDescending(i => i.Priority).First())
            .OrderByDescending(i => i.Priority).ThenBy(i => i.Text, StringComparer.OrdinalIgnoreCase).Take(200);
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // CompletionWindow supplies an anchored segment; its offsets change as the document changes.
        int startOffset = completionSegment.Offset;
        int length = completionSegment.Length;
        using (textArea.Document.RunUpdate())
        {
            textArea.Document.Remove(startOffset, length);
            textArea.Caret.Offset = startOffset;
            if (template == null) { textArea.Document.Insert(startOffset, Text); textArea.Caret.Offset = startOffset + Text.Length; return; }
            var snippet = new Snippet();
            int offset = 0;
            while (offset < template.Length)
            {
                int start = template.IndexOf("${", offset, StringComparison.Ordinal);
                if (start < 0) { snippet.Elements.Add(new SnippetTextElement { Text = template[offset..] }); break; }
                if (start > offset) snippet.Elements.Add(new SnippetTextElement { Text = template[offset..start] });
                int end = template.IndexOf('}', start);
                snippet.Elements.Add(new SnippetReplaceableTextElement { Text = template[(start + 2)..end] });
                offset = end + 1;
            }
            snippet.Insert(textArea);
        }
    }
}
