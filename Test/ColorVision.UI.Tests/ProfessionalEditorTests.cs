using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.Themes;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using AvalonTextEditor = ICSharpCode.AvalonEdit.TextEditor;

namespace ColorVision.UI.Tests;

public class ProfessionalEditorTests
{
    [Fact]
    public async Task SymbolNavigationAndJsonErrorLocationWorkThroughTheSharedControl()
    {
        await WpfTestHost.Invoke<Task>(async () =>
        {
            using var control = new AvalonEditControll(new EditorPreferences());
            var editor = Part<AvalonTextEditor>(control, "textEditor");
            editor.Text = "class Sample:\n    def run(self):\n        return 1\n\nSample().run()";
            Part<ComboBox>(control, "highlightingComboBox").SelectedItem = HighlightingManager.Instance.GetDefinition("Python");
            var window = Host(control);
            try
            {
                var list = Part<ListBox>(control, "SymbolList");
                var loaded = new TaskCompletionSource();
                var descriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ListBox));
                EventHandler changed = (_, _) => { if (list.Items.Count == 2) loaded.TrySetResult(); };
                descriptor.AddValueChanged(list, changed);
                try
                {
                    Part<Button>(control, "SymbolsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally { descriptor.RemoveValueChanged(list, changed); }
                Part<TextBox>(control, "SymbolQuery").Text = "run";
                Assert.Single(list.Items.Cast<EditorSymbol>());
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
                Assert.Equal(2, editor.TextArea.Caret.Line);
                Assert.Equal(9, editor.TextArea.Caret.Column);
                Assert.Equal(Visibility.Collapsed, Part<Border>(control, "SymbolPanel").Visibility);
                editor.Text = "{\n  \"name\" 1\n}";
                var diagnostic = Part<Button>(control, "DiagnosticButton");
                var reported = new TaskCompletionSource();
                DependencyPropertyChangedEventHandler visible = (_, _) => { if (diagnostic.IsVisible) reported.TrySetResult(); };
                diagnostic.IsVisibleChanged += visible;
                try
                {
                    Part<ComboBox>(control, "highlightingComboBox").SelectedItem = HighlightingManager.Instance.GetDefinition("Json");
                    await reported.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally { diagnostic.IsVisibleChanged -= visible; }
                Assert.Equal(2, Part<EditorMinimap>(control, "Minimap").ErrorLine);
                diagnostic.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(2, editor.TextArea.Caret.Line);
                editor.Text = "{}";
                Assert.Equal(Visibility.Collapsed, diagnostic.Visibility);
                Assert.Null(Part<EditorMinimap>(control, "Minimap").ErrorLine);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void SharedCodeAnalysisIgnoresStringsAndCommentsAndFindsRealSymbols()
    {
        var document = new TextDocument("# def fake():\nclass Actual:\n    def run(self, value):\n        text = 'ghost_identifier { }'\n        return (value + 1)\n");
        var analysis = new EditorCodeAnalysis("Python");
        EditorFoldingStrategy.CreateFoldings(document, HighlightingManager.Instance.GetDefinition("Python"), 4, analysis);
        var model = analysis.Complete(document);
        Assert.Equal(new[] { "Actual", "run" }, model.Symbols.Select(s => s.Name));
        Assert.DoesNotContain("ghost_identifier", model.Words);
        Assert.DoesNotContain("fake", model.Words);
        int opening = document.Text.IndexOf("(value", StringComparison.Ordinal);
        Assert.Equal(document.Text.IndexOf(')', opening), model.Brackets[opening]);
    }

    [Fact]
    public void CSharpOutlineDoesNotMistakeReturnCallsForMethodDeclarations()
    {
        var document = new TextDocument("public class Service\n{\n    public string Run(int count)\n    {\n        return GetValue(count);\n    }\n}");
        var analysis = new EditorCodeAnalysis("C#");
        EditorFoldingStrategy.CreateFoldings(document, HighlightingManager.Instance.GetDefinition("C#"), 4, analysis);
        Assert.Equal(new[] { "Service", "Run" }, analysis.Complete(document).Symbols.Select(s => s.Name));
    }

    [Fact]
    public void JsonDiagnosticsReferToTheCurrentDocumentAndDisappearAfterRepair()
    {
        var document = new TextDocument("{\n  \"threshold\" 0.95\n}");
        var analysis = new EditorCodeAnalysis("Json");
        var error = Assert.IsType<EditorDiagnostic>(analysis.Complete(document).Diagnostic);
        Assert.Equal(2, error.Line);
        Assert.Equal(error.Line, document.GetLineByOffset(error.Offset).LineNumber);
        document.Text = "{\n  \"threshold\": 0.95\n}";
        Assert.Null(new EditorCodeAnalysis("Json").Complete(document).Diagnostic);
    }

    [Theory]
    [InlineData("(", ")")]
    [InlineData("[", "]")]
    [InlineData("{", "}")]
    [InlineData("\"", "\"")]
    public void TypedPairsSkipOnlyTheGeneratedCloserAndUndoAsOneEdit(string opening, string closing)
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor { SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Python") };
            using var assistance = new EditorTypingAssistance(editor, new EditorPreferences(), () => "Python");
            editor.TextArea.PerformTextInput(opening);
            Assert.Equal(opening + closing, editor.Text);
            Assert.Equal(1, editor.CaretOffset);
            editor.TextArea.PerformTextInput(closing);
            Assert.Equal(opening + closing, editor.Text);
            Assert.Equal(2, editor.CaretOffset);
            editor.Undo();
            Assert.Equal("", editor.Text);
            editor.Text = closing;
            editor.CaretOffset = 0;
            editor.TextArea.PerformTextInput(closing);
            Assert.Equal(closing + closing, editor.Text);
        });
    }

    [Fact]
    public void PairDeletionAndSelectionWrappingPreserveUndoAndSelection()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor();
            using var assistance = new EditorTypingAssistance(editor, new EditorPreferences(), () => "Python");
            editor.TextArea.PerformTextInput("(");
            Assert.True(assistance.HandleEditingKey(Key.Back));
            Assert.Equal("", editor.Text);
            editor.Undo();
            Assert.Equal("()", editor.Text);
            editor.Text = "value";
            editor.Select(0, 5);
            editor.TextArea.PerformTextInput("[");
            Assert.Equal("[value]", editor.Text);
            Assert.Equal("value", editor.SelectedText);
            editor.Undo();
            Assert.Equal("value", editor.Text);
        });
    }

    [Theory]
    [InlineData("# note ", "(", "# note (")]
    [InlineData("value = \"text ", "(", "value = \"text (")]
    [InlineData("message = f", "\"", "message = f\"\"")]
    public void PairingRespectsCommentsStringsAndPythonStringPrefixes(string initial, string input, string expected)
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor { SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Python"), Text = initial };
            using var assistance = new EditorTypingAssistance(editor, new EditorPreferences(), () => "Python");
            editor.CaretOffset = initial.Length;
            editor.TextArea.PerformTextInput(input);
            Assert.Equal(expected, editor.Text);
        });
    }

    [Fact]
    public void PythonTripleQuotesProduceACompleteDocstringAndCanBeDeletedTogether()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor { SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Python") };
            using var assistance = new EditorTypingAssistance(editor, new EditorPreferences(), () => "Python");
            for (int i = 0; i < 3; i++) editor.TextArea.PerformTextInput("\"");
            Assert.Equal("\"\"\"\"\"\"", editor.Text);
            Assert.Equal(3, editor.CaretOffset);
            Assert.True(assistance.HandleEditingKey(Key.Back));
            Assert.Equal("", editor.Text);
        });
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void EnterBetweenBracesKeepsIndentationAndNewlineStyle(string newline)
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor { Text = "start" + newline + "    {}" };
            editor.Options.IndentationSize = 4;
            editor.Options.ConvertTabsToSpaces = true;
            using var assistance = new EditorTypingAssistance(editor, new EditorPreferences(), () => "C#");
            editor.CaretOffset = editor.Document.TextLength - 1;
            Assert.True(assistance.HandleEditingKey(Key.Enter));
            Assert.Equal("start" + newline + "    {" + newline + "        " + newline + "    }", editor.Text);
            Assert.Equal(3, editor.TextArea.Caret.Line);
            Assert.Equal(9, editor.TextArea.Caret.Column);
            editor.Undo();
            Assert.Equal("start" + newline + "    {}", editor.Text);
        });
    }

    [Fact]
    public void CompletionUsesDocumentWordsAndDoesNotOfferForeignLanguageSnippets()
    {
        var model = new EditorCodeModel(["threshold", "thresholds"], [], new Dictionary<int, int>(), null);
        Assert.Equal(new[] { "threshold", "thresholds" }, EditorCompletionItem.Create("Python", model, "thre", true).Select(i => i.Text));
        Assert.DoesNotContain(EditorCompletionItem.Create("JavaScript", model, "for", true), i => i.Text == "foreach");
    }

    [Fact]
    public void SnippetCompletionUsesEditablePlaceholdersAndIsOneUndoGroup()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor { Text = "    de" };
            editor.Options.IndentationSize = 4;
            editor.Options.ConvertTabsToSpaces = true;
            editor.CaretOffset = editor.Document.TextLength;
            var window = Host(editor);
            try
            {
                var completion = Assert.Single(EditorCompletionItem.Create("Python", EditorCodeModel.Empty, "def", true));
                completion.Complete(editor.TextArea, new TextSegment { StartOffset = 4, Length = 2 }, EventArgs.Empty);
                Assert.Contains("def name(args):", editor.Text);
                Assert.Contains("        pass", editor.Text);
                Assert.Equal("name", editor.SelectedText);
                editor.Undo();
                Assert.Equal("    de", editor.Text);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ActualCompletionWindowAcceptsAWordAndClosesWhenTheEditorHides()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor { Text = "thre" };
            using var assistance = new EditorTypingAssistance(editor, new EditorPreferences(), () => "Python");
            assistance.SetModel(new EditorCodeModel(["threshold"], [], new Dictionary<int, int>(), null));
            editor.CaretOffset = 4;
            var window = Host(editor);
            try
            {
                assistance.ShowCompletion(true);
                Assert.NotNull(assistance.Completion);
                assistance.Completion.CompletionList.RequestInsertion(EventArgs.Empty);
                Assert.Equal("threshold", editor.Text);
                Assert.Null(assistance.Completion);
                editor.Undo();
                Assert.Equal("thre", editor.Text);
                editor.CaretOffset = 4;
                assistance.ShowCompletion(true);
                Assert.NotNull(assistance.Completion);
                editor.Visibility = Visibility.Collapsed;
                Assert.Null(assistance.Completion);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData("if ready: # comment\n", "    ")]
    [InlineData("text = 'literal:'\n", "")]
    [InlineData("    if ready:\n", "        ")]
    public void PythonIndentationUsesCodeInsteadOfColonsInStrings(string source, string expectedIndent)
    {
        var document = new TextDocument(source);
        using var highlighter = new DocumentHighlighter(document, HighlightingManager.Instance.GetDefinition("Python"));
        var strategy = new PythonIndentationStrategy(() => "    ", () => highlighter);
        strategy.IndentLine(document, document.GetLineByNumber(2));
        Assert.Equal(expectedIndent, document.GetText(document.GetLineByNumber(2)));
    }

    [Fact]
    public async Task FoldingUsesAnIndependentSnapshotAndHonorsCancellation()
    {
        var document = new TextDocument("def run():\n    return 1\n");
        var snapshot = document.CreateSnapshot();
        var definition = HighlightingManager.Instance.GetDefinition("Python");
        document.Text = "changed";
        var result = await Task.Run(() => EditorFoldingStrategy.CreateFoldings(new TextDocument(snapshot), definition, 4));
        Assert.Single(result);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => EditorFoldingStrategy.CreateFoldings(new TextDocument(snapshot), definition, 4, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void SearchTakesKeyboardFocusAfterOpeningAndDoesNotDirtyTheDocument()
    {
        WpfTestHost.Invoke(() =>
        {
            using var control = new AvalonEditControll(new EditorPreferences());
            var editor = Part<AvalonTextEditor>(control, "textEditor");
            editor.Text = "unchanged";
            editor.Document.UndoStack.MarkAsOriginalFile();
            var window = Host(control);
            try
            {
                window.Activate();
                editor.Focus();
                var bar = Part<EditorSearchBar>(control, "SearchBar");
                bar.Open(false);
                Drain();
                Assert.Same(Part<TextBox>(bar, "QueryBox"), Keyboard.FocusedElement);
                Assert.Equal("unchanged", editor.Text);
                Assert.False(control.IsDirty);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PythonFoldsNestedBlocksAndMultilineHeadersWithoutReadingStringsAsCode()
    {
        var document = new TextDocument("def outer(\n    value,\n):\n    text = 'not: { }'\n    # if fake:\n    if value:\n        return value\n    return None\n\nprint('done')\n");
        var folds = EditorFoldingStrategy.CreateFoldings(document, HighlightingManager.Instance.GetDefinition("Python"), 4);
        Assert.Equal(2, folds.Count);
        Assert.Equal(document.GetLineByNumber(1).EndOffset, folds[0].StartOffset);
        Assert.Equal(document.GetLineByNumber(8).EndOffset, folds[0].EndOffset);
        Assert.Equal(document.GetLineByNumber(6).EndOffset, folds[1].StartOffset);
        Assert.Equal(document.GetLineByNumber(7).EndOffset, folds[1].EndOffset);
    }

    [Fact]
    public void PythonDocstringOnlyBodyStillFolds()
    {
        var document = new TextDocument("def help():\n    \"\"\"説明\nnot code: {\n    \"\"\"\n\nprint('done')");
        var fold = Assert.Single(EditorFoldingStrategy.CreateFoldings(document, HighlightingManager.Instance.GetDefinition("Python"), 4));
        Assert.Equal(document.GetLineByNumber(4).EndOffset, fold.EndOffset);
    }

    [Fact]
    public void BraceFoldingIgnoresBracesInsideCommentsAndStrings()
    {
        var document = new TextDocument("class Example {\n    // }\n    string text = \"}\";\n    void Run() {\n        Call();\n    }\n}\n");
        var folds = EditorFoldingStrategy.CreateFoldings(document, HighlightingManager.Instance.GetDefinition("C#"), 4);
        Assert.Equal(2, folds.Count);
        Assert.Equal(document.GetLineByNumber(7).Offset, folds[0].EndOffset);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void MoveLastLinePreservesDelimitersAndUndoRestoresOriginal(string newline)
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor { Text = $"first{newline}second{newline}third" };
            editor.CaretOffset = editor.Document.GetLineByNumber(3).Offset + 2;
            EditorTextOperations.MoveLines(editor, -1);
            Assert.Equal($"first{newline}third{newline}second", editor.Text);
            Assert.Equal(2, editor.TextArea.Caret.Line);
            Assert.Equal(3, editor.TextArea.Caret.Column);
            editor.Undo();
            Assert.Equal($"first{newline}second{newline}third", editor.Text);
        });
    }

    [Fact]
    public void CommentExcludesNextLineAtSelectionBoundaryAndIsOneUndoStep()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor { Text = "    one\n    two\n    three" };
            editor.Select(0, editor.Document.GetLineByNumber(3).Offset);
            EditorTextOperations.ToggleComment(editor, "#");
            Assert.Equal("    # one\n    # two\n    three", editor.Text);
            editor.Undo();
            Assert.Equal("    one\n    two\n    three", editor.Text);
        });
    }

    [Fact]
    public void DuplicateLastLineKeepsCaretColumnAndNewlineConvention()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new AvalonTextEditor { Text = "first\r\nlast" };
            editor.CaretOffset = editor.Document.TextLength - 1;
            EditorTextOperations.Duplicate(editor);
            Assert.Equal("first\r\nlast\r\nlast", editor.Text);
            Assert.Equal(3, editor.TextArea.Caret.Line);
            Assert.Equal(4, editor.TextArea.Caret.Column);
            editor.Undo();
            Assert.Equal("first\r\nlast", editor.Text);
        });
    }

    [Fact]
    public void SearchSupportsCaseWordRegexAndLiteralReplacement()
    {
        Assert.Equal(2, EditorSearchBar.FindMatches("Value value values", "value", false, true, false).Count);
        Assert.Single(EditorSearchBar.FindMatches("Value value values", "value", true, true, false));
        var match = Assert.Single(EditorSearchBar.FindMatches("id=42", @"id=(\d+)", true, false, true));
        Assert.Equal("number=42", EditorSearchBar.Replacement(match, "number=$1", true));
        Assert.Equal("$1", EditorSearchBar.Replacement(match, "$1", false));
        Assert.Throws<RegexParseException>(() => EditorSearchBar.FindMatches("test", "[", false, false, true));
    }

    [Fact]
    public void ExcessiveSearchMatchesCannotProduceAPartialReplaceAll()
    {
        Assert.Throws<InvalidOperationException>(() => EditorSearchBar.FindMatches(new string('x', 20_001), "x", true, false, false));
    }

    [Fact]
    public void DiskJsonIsNotReformattedAndUndoReturnsToCleanState()
    {
        string file = Path.Combine(Path.GetTempPath(), $"editor-{Guid.NewGuid():N}.json");
        const string original = "{\"a\":1, \"b\":2}";
        File.WriteAllText(file, original);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                using var control = new AvalonEditControll(new EditorPreferences());
                Assert.True(control.OpenFile(file));
                var editor = Part<AvalonTextEditor>(control, "textEditor");
                Assert.Equal(original, editor.Text);
                Assert.False(control.IsDirty);
                editor.AppendText(" ");
                Assert.True(control.IsDirty);
                editor.Undo();
                Assert.False(control.IsDirty);
                Assert.Equal(original, File.ReadAllText(file));
            });
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void MapNavigationPreservesSelectionAndReusesDrawingAcrossScrollAndWrap()
    {
        WpfTestHost.Invoke(() =>
        {
            using var control = new AvalonEditControll(new EditorPreferences());
            var editor = Part<AvalonTextEditor>(control, "textEditor");
            editor.Text = string.Join('\n', Enumerable.Range(1, 700).Select(n => $"line {n}: " + new string('x', 100)));
            var window = Host(control);
            try
            {
                Drain();
                var map = Part<EditorMinimap>(control, "Minimap");
                Assert.True(map.RenderCount > 0);
                editor.Select(20, 8);
                int caret = editor.CaretOffset;
                int renderCount = map.RenderCount;
                map.ScrollToMapPosition(map.ActualHeight * .7, true);
                Drain();
                Assert.True(editor.VerticalOffset > 0);
                Assert.Equal(caret, editor.CaretOffset);
                Assert.Equal(20, editor.SelectionStart);
                Assert.Equal(8, editor.SelectionLength);
                Assert.Equal(renderCount, map.RenderCount);
                Assert.True(map.ViewportRectangle.Top > 0);
                editor.WordWrap = true;
                window.Width = 640;
                Drain();
                map.ScrollToMapPosition(map.ActualHeight * (280.25 / 700), true);
                Drain();
                int visible = editor.TextArea.TextView.GetDocumentLineByVisualTop(editor.VerticalOffset + editor.ViewportHeight / 2).LineNumber;
                Assert.Equal(281, visible);
                map.ShowCodePreview(map.ActualHeight * .8);
                Drain();
                Assert.Equal(caret, editor.CaretOffset);
                Assert.Equal(8, editor.SelectionLength);
                map.HidePreview();
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void SearchReplaceAllIsUndoableThroughTheActualBar()
    {
        WpfTestHost.Invoke(() =>
        {
            using var control = new AvalonEditControll(new EditorPreferences());
            var editor = Part<AvalonTextEditor>(control, "textEditor");
            editor.Text = "one two one\nOne";
            var window = Host(control);
            try
            {
                var search = Part<EditorSearchBar>(control, "SearchBar");
                search.Open(true);
                Part<TextBox>(search, "QueryBox").Text = "one";
                Part<TextBox>(search, "ReplacementBox").Text = "$value";
                search.FindNext();
                Assert.Equal(3, search.Matches.Count);
                Part<Button>(search, "ReplaceAllButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("$value two $value\n$value", editor.Text);
                editor.Undo();
                Assert.Equal("one two one\nOne", editor.Text);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void LargeDocumentMapRemainsNavigableAfterEditing(int lineCount)
    {
        WpfTestHost.Invoke(() =>
        {
            using var control = new AvalonEditControll(new EditorPreferences());
            var editor = Part<AvalonTextEditor>(control, "textEditor");
            editor.Text = string.Join('\n', Enumerable.Range(1, lineCount).Select(n => $"value_{n} = calculate_region({n}, threshold=0.95) # 区域检测"));
            Part<ComboBox>(control, "highlightingComboBox").SelectedItem = HighlightingManager.Instance.GetDefinition("Python");
            var window = Host(control);
            try
            {
                Drain();
                var map = Part<EditorMinimap>(control, "Minimap");
                Assert.True(map.RenderCount > 0);
                int target = lineCount * 3 / 4;
                map.ScrollToMapPosition(map.ActualHeight * ((target - .75) / lineCount), true);
                Drain();
                var view = editor.TextArea.TextView;
                Assert.Equal(target, view.GetDocumentLineByVisualTop(editor.VerticalOffset + editor.ViewportHeight / 2).LineNumber);
                int rendered = map.RenderCount;
                int offset = editor.Document.GetLineByNumber(target).EndOffset;
                editor.Document.Insert(offset, " # edited");
                Drain();
                Assert.True(map.RenderCount > rendered);
                Assert.EndsWith(" # edited", editor.Document.GetText(editor.Document.GetLineByNumber(target)));
                editor.Undo();
                Assert.DoesNotContain("# edited", editor.Document.GetText(editor.Document.GetLineByNumber(target)));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void HiddenEditorResumesWithCurrentTextAndViewPreferences()
    {
        WpfTestHost.Invoke(() =>
        {
            var preferences = new EditorPreferences();
            using var control = new AvalonEditControll(preferences);
            var editor = Part<AvalonTextEditor>(control, "textEditor");
            editor.Text = string.Join('\n', Enumerable.Range(1, 300));
            var window = Host(control);
            try
            {
                Drain();
                var map = Part<EditorMinimap>(control, "Minimap");
                control.Visibility = Visibility.Collapsed;
                Drain();
                int renderCount = map.RenderCount;
                editor.AppendText("\nchanged while hidden");
                Drain();
                Assert.Equal(renderCount, map.RenderCount);
                preferences.Font = EditorFont.Consolas;
                preferences.FontSize = 16;
                control.Visibility = Visibility.Visible;
                Drain();
                Assert.True(map.RenderCount > renderCount);
                Assert.Equal("Consolas, Microsoft YaHei UI", editor.FontFamily.Source);
                Assert.Equal(16, editor.FontSize);
                control.SetZoom(1.25);
                Assert.Equal(20, editor.FontSize);
                Assert.Equal(16, preferences.FontSize);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void SearchResultsRefreshAfterTheEditorWasHiddenDuringAnEdit()
    {
        WpfTestHost.Invoke(() =>
        {
            using var control = new AvalonEditControll(new EditorPreferences());
            var editor = Part<AvalonTextEditor>(control, "textEditor");
            editor.Text = "find me";
            var window = Host(control);
            try
            {
                var search = Part<EditorSearchBar>(control, "SearchBar");
                search.Open(false);
                Part<TextBox>(search, "QueryBox").Text = "find";
                search.FindNext();
                Assert.Single(search.Matches);
                control.Visibility = Visibility.Collapsed;
                editor.AppendText(" and find again");
                control.Visibility = Visibility.Visible;
                Drain();
                Assert.Equal(2, search.Matches.Count);
            }
            finally { window.Close(); }
        });
    }

    private static T Part<T>(FrameworkElement control, string name) => Assert.IsType<T>(control.FindName(name));
    private static Window Host(FrameworkElement content)
    {
        var window = new Window { Content = content, Width = 1100, Height = 700, Left = -16000, Top = -16000, ShowInTaskbar = false, ShowActivated = false };
        foreach (string source in ThemeManager.ResourceDictionaryDark.Concat(ThemeManager.ResourceDictionaryBase))
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
