using ICSharpCode.AvalonEdit.Document;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution.Editor.AvalonEditor;

public partial class AvalonEditControll
{
    private readonly Stack<TextAnchor> _navigationBack = new();
    private readonly Stack<TextAnchor> _navigationForward = new();
    private bool _restoringNavigation;

    private void ApplyCodeModel(EditorCodeModel model)
    {
        _typingAssistance?.SetModel(model);
        Minimap.ErrorLine = model.Diagnostic?.Line;
        DiagnosticButton.Visibility = model.Diagnostic != null ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticButton.Content = model.Diagnostic == null ? "" : $"JSON 语法问题 · {model.Diagnostic.Line}:{model.Diagnostic.Column}";
        DiagnosticButton.ToolTip = model.Diagnostic?.Message;
        if (SymbolPanel.Visibility == Visibility.Visible) FilterSymbols();
    }

    private void SearchMatchesChanged(object? sender, EventArgs e)
    {
        Minimap.SetSearchLines(SearchBar.Matches.Select(m => textEditor.Document.GetLineByOffset(Math.Clamp(m.Index, 0, textEditor.Document.TextLength)).LineNumber));
    }

    private bool HandleCodeNavigationKey(KeyEventArgs e, ModifierKeys modifiers)
    {
        if (modifiers == ModifierKeys.Control && e.Key == Key.F12 || modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.O)
            OpenSymbols();
        else if (textEditor.IsKeyboardFocusWithin && modifiers == ModifierKeys.Control && e.Key == Key.Space)
            _typingAssistance?.ShowCompletion(true);
        else if (textEditor.IsKeyboardFocusWithin && modifiers == ModifierKeys.None && e.Key == Key.F12)
            GoToDefinition();
        else if (textEditor.IsKeyboardFocusWithin && modifiers == ModifierKeys.Control && e.Key == Key.OemCloseBrackets)
        {
            var brackets = _typingAssistance?.Model.Brackets;
            int offset = textEditor.CaretOffset;
            if (brackets != null && (brackets.TryGetValue(offset, out int match) || brackets.TryGetValue(offset - 1, out match)))
            {
                RememberNavigation();
                textEditor.CaretOffset = match;
                textEditor.TextArea.Caret.BringCaretToView();
            }
        }
        else if (modifiers == ModifierKeys.Alt && e.SystemKey is Key.Left or Key.Right)
            RestoreNavigation(e.SystemKey == Key.Left);
        else return false;
        e.Handled = true;
        return true;
    }

    private async void OpenSymbols()
    {
        _typingAssistance?.CloseCompletion();
        Minimap.HidePreview();
        GoToPanel.Visibility = Visibility.Collapsed;
        SymbolPanel.Width = Math.Clamp(ActualWidth - 32, 240, 520);
        SymbolPanel.Visibility = Visibility.Visible;
        SymbolQuery.Text = "";
        FilterSymbols();
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { if (SymbolPanel.IsVisible) Keyboard.Focus(SymbolQuery); });
        _foldingPending = true;
        await UpdateFoldingsAsync();
    }

    private void FilterSymbols()
    {
        if (SymbolList == null || _typingAssistance == null) return;
        var symbols = _typingAssistance.Model.Symbols.Where(s => s.Name.Contains(SymbolQuery.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
        SymbolList.ItemsSource = symbols;
        SymbolList.SelectedIndex = symbols.Length > 0 ? 0 : -1;
        SymbolHint.Text = symbols.Length == 0 ? "没有匹配的文档声明" : $"{symbols.Length} 个声明 · Enter 跳转 · Esc 关闭";
    }

    private void SymbolQuery_TextChanged(object sender, TextChangedEventArgs e) => FilterSymbols();
    private void Symbols_Click(object sender, RoutedEventArgs e) => OpenSymbols();
    private void SymbolList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => NavigateToSelectedSymbol();
    private void SymbolPanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { SymbolPanel.Visibility = Visibility.Collapsed; textEditor.Focus(); }
        else if (e.Key == Key.Enter) NavigateToSelectedSymbol();
        else if (e.Key is Key.Up or Key.Down)
        {
            SymbolList.SelectedIndex = Math.Clamp(SymbolList.SelectedIndex + (e.Key == Key.Up ? -1 : 1), 0, Math.Max(0, SymbolList.Items.Count - 1));
            if (SymbolList.SelectedItem != null) SymbolList.ScrollIntoView(SymbolList.SelectedItem);
        }
        else return;
        e.Handled = true;
    }

    private void NavigateToSelectedSymbol()
    {
        if (SymbolList.SelectedItem is not EditorSymbol symbol || _typingAssistance?.Model.Symbols.Contains(symbol) != true) return;
        SymbolPanel.Visibility = Visibility.Collapsed;
        NavigateTo(symbol.Line, symbol.Offset - textEditor.Document.GetLineByNumber(symbol.Line).Offset + 1);
    }

    private async void GoToDefinition()
    {
        int offset = textEditor.CaretOffset;
        if (_typingAssistance?.IsNonCode(offset) == true) return;
        string name = EditorCodeAnalysis.WordAt(textEditor.Document, offset).Word;
        var version = textEditor.Document.Version;
        _foldingPending = true;
        await UpdateFoldingsAsync();
        if (_disposed || !ReferenceEquals(version, textEditor.Document.Version) || textEditor.CaretOffset != offset) return;
        var candidates = _typingAssistance!.Model.Symbols.Where(s => s.Name == name).ToArray();
        if (candidates.Length == 1)
        {
            var symbol = candidates[0];
            NavigateTo(symbol.Line, symbol.Offset - textEditor.Document.GetLineByNumber(symbol.Line).Offset + 1);
        }
        else
        {
            OpenSymbols();
            SymbolQuery.Text = name;
            if (candidates.Length == 0) SymbolHint.Text = "未找到文档内定义；可搜索其他声明";
        }
    }

    private void Diagnostic_Click(object sender, RoutedEventArgs e)
    {
        if (_typingAssistance?.Model.Diagnostic is { } diagnostic) NavigateTo(diagnostic.Line, diagnostic.Column);
    }

    private void RememberNavigation()
    {
        if (_restoringNavigation) return;
        _navigationBack.Push(textEditor.Document.CreateAnchor(textEditor.CaretOffset));
        _navigationForward.Clear();
    }

    private void RestoreNavigation(bool backward)
    {
        var source = backward ? _navigationBack : _navigationForward;
        var destination = backward ? _navigationForward : _navigationBack;
        while (source.TryPop(out var anchor))
        {
            if (anchor.IsDeleted || anchor.Offset == textEditor.CaretOffset) continue;
            destination.Push(textEditor.Document.CreateAnchor(textEditor.CaretOffset));
            _restoringNavigation = true;
            try
            {
                var position = textEditor.Document.GetLocation(anchor.Offset);
                NavigateTo(position.Line, position.Column);
            }
            finally { _restoringNavigation = false; }
            return;
        }
    }

    private void ResetCodeNavigation()
    {
        _navigationBack.Clear();
        _navigationForward.Clear();
        _typingAssistance?.Reset();
        SymbolPanel.Visibility = Visibility.Collapsed;
    }
}
