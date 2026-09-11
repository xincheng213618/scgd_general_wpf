using ColorVision.Themes;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons;

public partial class EditTemplateJson
{
    private SearchPanel? _searchPanel;

    private void InitializeTextTools()
    {
        _searchPanel = SearchPanel.Install(textEditor.TextArea);
        textEditor.Options.HighlightCurrentLine = true;
        textEditor.Options.ConvertTabsToSpaces = true;
        textEditor.Options.IndentationSize = 2;
        textEditor.TextArea.TextView.SetResourceReference(TextView.CurrentLineBackgroundProperty, "EditorCurrentLineBrush");
        textEditor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "EditorSelectionBrush");
        textEditor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionForegroundProperty, "EditorSelectionForegroundBrush");
        textEditor.TextArea.TextView.LineTransformers.Add(new JsonSyntaxColors(textEditor));
        textEditor.TextArea.Caret.PositionChanged += (_, _) => UpdatePosition();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && _isInPropertyEditorMode)
            {
                propertyEditor.FocusSearch();
                e.Handled = true;
            }
        };
    }

    private void EditorThemeChanged(Theme theme)
    {
        textEditor.TextArea.TextView.Redraw();
        if (TryFindResource("EditorCaretBrush") is Brush brush) textEditor.TextArea.Caret.CaretBrush = brush;
    }

    private void SetTextSilently(string json)
    {
        _isSyncingFromPropertyEditor = true;
        try { textEditor.Text = json ?? ""; }
        finally { _isSyncingFromPropertyEditor = false; }
    }

    private bool TryReadDraft(out JsonValueKind kind, out string error)
    {
        kind = JsonValueKind.Undefined;
        error = "";
        try
        {
            using var document = JsonDocument.Parse(textEditor.Text);
            kind = document.RootElement.ValueKind;
            if (kind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                error = "模板需要 JSON 对象或数组。";
                return false;
            }
            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON 语法错误 · 第 {ex.LineNumber + 1} 行，第 {ex.BytePositionInLine + 1} 字节";
            StatusText.ToolTip = ex.Message;
            return false;
        }
    }

    public bool TryCommitPendingEdits()
    {
        if (IEditTemplateJson == null) return true;
        if (_isInPropertyEditorMode && !propertyEditor.ValidateJson())
        {
            ShowStatusError("有错误输入尚未应用，请先修正后再保存或切换模板。");
            return false;
        }
        if (!TryReadDraft(out _, out var error))
        {
            ShowStatusError(error + "。草稿已保留，请修正后再保存或切换模板。");
            return false;
        }
        IEditTemplateJson.JsonValue = textEditor.Text;
        return true;
    }

    public void AcceptSavedChanges()
    {
        _loadedJsonSnapshot = textEditor.Text;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (StatusText == null || IEditTemplateJson == null) return;
        if (_isInPropertyEditorMode && propertyEditor.HasValidationErrors) { ShowStatusError("有错误输入尚未应用"); return; }
        if (!TryReadDraft(out _, out var error)) { ShowStatusError(error + " · 草稿尚未应用"); return; }
        bool modified;
        try
        {
            using var original = JsonDocument.Parse(_loadedJsonSnapshot);
            using var current = JsonDocument.Parse(textEditor.Text);
            modified = JsonSerializer.Serialize(original.RootElement) != JsonSerializer.Serialize(current.RootElement);
        }
        catch (JsonException) { modified = true; }
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
        StatusText.Text = modified ? "● 已修改 · Ctrl+S 保存" : "语法正确 · 无新修改";
        StatusText.ToolTip = "语法检查不等于算法参数有效性检查。";
        UpdatePosition();
    }

    private void ShowStatusError(string message)
    {
        StatusText.Text = message;
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
    }

    private void UpdatePosition() => PositionText.Text = $"行 {textEditor.TextArea.Caret.Line}，列 {textEditor.TextArea.Caret.Column} · {textEditor.LineCount} 行";

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (TryCommitPendingEdits())
        {
            UpdateStatus();
            StatusText.Text = "语法检查通过 · " + StatusText.Text;
        }
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCommitPendingEdits()) return;
        var formatted = UI.Utilities.JsonHelper.BeautifyJson(textEditor.Text);
        if (formatted == textEditor.Text) return;
        using (textEditor.Document.RunUpdate())
            textEditor.Document.Replace(0, textEditor.Document.TextLength, formatted);
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        _searchPanel?.Open();
        _searchPanel?.Reactivate();
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button) { menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true; }
    }

    private void CopyJson_Click(object sender, RoutedEventArgs e)
    {
        if (TryCommitPendingEdits()) Common.Clipboard.SetText(textEditor.Text);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (IEditTemplateJson == null) return;
        if (MessageBox.Show(Window.GetWindow(this), "用默认参数替换当前内容？此操作会丢弃当前草稿，保存后才写入模板。", "恢复默认参数", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        IEditTemplateJson.ResetCommand.Execute(null);
    }

    private sealed class JsonSyntaxColors(ICSharpCode.AvalonEdit.TextEditor editor) : DocumentColorizingTransformer
    {
        private static readonly Regex Tokens = new("\"(?:[^\"\\\\]|\\\\.)*\"|\\b(?:true|false|null)\\b|-?\\b\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?", RegexOptions.Compiled);
        protected override void ColorizeLine(ICSharpCode.AvalonEdit.Document.DocumentLine line)
        {
            string text = CurrentContext.Document.GetText(line);
            foreach (Match match in Tokens.Matches(text))
            {
                var key = match.Value.StartsWith('"')
                    ? text[(match.Index + match.Length)..].TrimStart().StartsWith(':') ? "EditorSyntaxPropertyBrush" : "EditorSyntaxStringBrush"
                    : char.IsLetter(match.Value[0]) ? "EditorSyntaxKeywordBrush" : "EditorSyntaxNumberBrush";
                if (editor.TryFindResource(key) is Brush brush)
                    ChangeLinePart(line.Offset + match.Index, line.Offset + match.Index + match.Length, element => element.TextRunProperties.SetForegroundBrush(brush));
            }
        }
    }
}
