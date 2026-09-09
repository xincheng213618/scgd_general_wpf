using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Solution.Editor.AvalonEditor;

public partial class AvalonEditControll
{
    private void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        if (_disposed) return;
        ApplyPreferences();
        _themeController?.SetActive(IsVisible);
        if (_foldingPending && IsVisible) _foldingUpdateTimer?.Start();
    }

    private void Editor_Unloaded(object sender, RoutedEventArgs e)
    {
        _typingAssistance?.CloseCompletion();
        _foldingUpdateTimer?.Stop();
        SuspendFoldingWork();
        _themeController?.SetActive(false);
    }

    private void Editor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_disposed) return;
        _themeController?.SetActive(IsVisible && IsLoaded);
        if (IsVisible && IsLoaded && _foldingPending) _foldingUpdateTimer?.Start();
        else { _foldingUpdateTimer?.Stop(); SuspendFoldingWork(); }
    }

    private void SuspendFoldingWork()
    {
        if (_foldingCancellation == null) return;
        _foldingPending = true;
        _foldingCancellation.Cancel();
    }

    private void ThemeColorsChanged(object? sender, EventArgs e) => Minimap.InvalidateDocument();
    private void UndoStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsOriginalFile") SetDirty(!textEditor.Document.UndoStack.IsOriginalFile);
    }

    private void PreferencesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(ApplyPreferences); return; }
        ApplyPreferences();
    }

    private void ApplyPreferences()
    {
        textEditor.FontFamily = new FontFamily(_preferences.FontFamilyName);
        textEditor.FontSize = _preferences.FontSize * _zoom;
        textEditor.FontWeight = FontWeights.Normal;
        textEditor.WordWrap = _preferences.WordWrap;
        textEditor.ShowLineNumbers = _preferences.ShowLineNumbers;
        textEditor.Options.IndentationSize = _preferences.IndentationSize;
        textEditor.Options.ConvertTabsToSpaces = _preferences.ConvertTabsToSpaces;
        textEditor.Options.ShowSpaces = textEditor.Options.ShowTabs = textEditor.Options.ShowEndOfLine = _preferences.ShowWhitespace;
        textEditor.Options.EnableHyperlinks = false;
        textEditor.Options.EnableEmailHyperlinks = false;
        Minimap.Width = _preferences.MinimapWidth;
        Minimap.ShowPreview = _preferences.ShowPreview;
        Minimap.BoldKeywords = _preferences.BoldKeywords;
        Minimap.Visibility = _preferences.ShowMinimap ? Visibility.Visible : Visibility.Collapsed;
        textEditor.VerticalScrollBarVisibility = _preferences.ShowMinimap ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
        _indentGuides.IsEnabled = _preferences.ShowIndentGuides;
        if (_themeController != null)
        {
            _themeController.BoldKeywords = _preferences.BoldKeywords;
            _themeController.RefreshTheme();
        }
        IndentationText.Text = $"{(_preferences.ConvertTabsToSpaces ? "空格" : "Tab")}: {_preferences.IndentationSize}";
        ZoomButton.Content = $"{_zoom:P0}";
        textEditor.TextArea.TextView.Redraw();
        if (_highlightingDefinition != null) ScheduleFoldings();
    }

    internal void SetZoom(double zoom)
    {
        if (!double.IsFinite(zoom)) return;
        _zoom = Math.Clamp(Math.Round(zoom, 2), .5, 2.5);
        textEditor.FontSize = _preferences.FontSize * _zoom;
        ZoomButton.Content = $"{_zoom:P0}";
        Minimap.HidePreview();
    }

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        SetZoom(_zoom + Math.Sign(e.Delta) * .1);
        e.Handled = true;
    }

    private void Zoom_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ZoomButton };
        foreach (int percent in new[] { 50, 75, 90, 100, 110, 125, 150, 200, 250 })
        {
            var item = new MenuItem { Header = $"{percent}%", IsCheckable = true, IsChecked = Math.Abs(_zoom - percent / 100.0) < .005 };
            item.Click += (_, _) => SetZoom(percent / 100.0);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new PropertyEditorWindow(_preferences) { Owner = Window.GetWindow(this), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.ShowDialog();
        PersistPreferences();
    }

    private void ViewOption_Click(object sender, RoutedEventArgs e) => PersistPreferences();

    private void PersistPreferences()
    {
        try
        {
            if (ConfigService.Instance is ConfigHandler handler)
            {
                if (!handler.TrySave(_preferences, out string error)) MessageBox.Show(Window.GetWindow(this), error, "编辑器设置保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else ConfigService.Instance?.Save<EditorPreferences>();
        }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "编辑器设置保存失败", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Find_Click(object sender, RoutedEventArgs e) => SearchBar.Open(false);

    private void OpenGoTo()
    {
        _typingAssistance?.CloseCompletion();
        SymbolPanel.Visibility = Visibility.Collapsed;
        Minimap.HidePreview();
        GoToPanel.Visibility = Visibility.Visible;
        GoToBox.Text = textEditor.TextArea.Caret.Line.ToString();
        GoToHint.Text = $"1–{textEditor.Document.LineCount:N0} 行 · 可输入 行:列";
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            if (GoToPanel.Visibility != Visibility.Visible) return;
            Keyboard.Focus(GoToBox);
            GoToBox.SelectAll();
        });
    }

    private void GoToBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            GoToPanel.Visibility = Visibility.Collapsed;
            textEditor.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            string[] parts = GoToBox.Text.Trim().Split(':', '：');
            int column = 1;
            if (parts.Length is < 1 or > 2 || !int.TryParse(parts[0], out int line) || line < 1 || line > textEditor.Document.LineCount
                || parts.Length == 2 && (!int.TryParse(parts[1], out column) || column < 1))
            {
                GoToHint.Text = $"请输入 1–{textEditor.Document.LineCount:N0} 范围内的行号";
                return;
            }
            GoToPanel.Visibility = Visibility.Collapsed;
            NavigateTo(line, column);
        }
    }

    private void InstallContextMenu()
    {
        var menu = new ContextMenu();
        foreach (var (title, command, shortcut) in new[]
        {
            ("撤销", ApplicationCommands.Undo, "Ctrl+Z"), ("重做", ApplicationCommands.Redo, "Ctrl+Y"),
            ("剪切", ApplicationCommands.Cut, "Ctrl+X"), ("复制", ApplicationCommands.Copy, "Ctrl+C"),
            ("粘贴", ApplicationCommands.Paste, "Ctrl+V"), ("全选", ApplicationCommands.SelectAll, "Ctrl+A")
        }) menu.Items.Add(new MenuItem { Header = title, Command = command, CommandTarget = textEditor.TextArea, InputGestureText = shortcut });
        menu.Items.Add(new Separator());
        AddAction("查找", "Ctrl+F", () => SearchBar.Open(false));
        AddAction("替换", "Ctrl+H", () => SearchBar.Open(true));
        AddAction("跳转到行", "Ctrl+G", OpenGoTo);
        AddAction("文档符号", "Ctrl+F12", OpenSymbols);
        AddAction("文档内定义", "F12", GoToDefinition);
        AddAction("显示补全", "Ctrl+Space", () => _typingAssistance?.ShowCompletion(true));
        menu.Items.Add(new Separator());
        var comment = AddAction("切换行注释", "Ctrl+/", () =>
        {
            if (EditorTextOperations.CommentPrefix(_highlightingDefinition?.Name) is { } prefix) EditorTextOperations.ToggleComment(textEditor, prefix);
        });
        AddAction("复制行 / 选区", "Ctrl+D", () => EditorTextOperations.Duplicate(textEditor));
        AddAction("上移行", "Alt+↑", () => EditorTextOperations.MoveLines(textEditor, -1));
        AddAction("下移行", "Alt+↓", () => EditorTextOperations.MoveLines(textEditor, 1));
        var format = AddAction("格式化 JSON", "", () =>
        {
            try { textEditor.Document.Replace(0, textEditor.Document.TextLength, JToken.Parse(textEditor.Text).ToString(Formatting.Indented)); }
            catch (JsonReaderException ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "JSON 格式错误", MessageBoxButton.OK, MessageBoxImage.Information); }
        });
        menu.Items.Add(new Separator());
        var collapse = AddAction("折叠全部", "", () => SetFoldings(true));
        var expand = AddAction("展开全部", "", () => SetFoldings(false));
        menu.Opened += (_, _) =>
        {
            comment.IsEnabled = EditorTextOperations.CommentPrefix(_highlightingDefinition?.Name) != null;
            format.Visibility = _highlightingDefinition?.Name == "Json" ? Visibility.Visible : Visibility.Collapsed;
            collapse.IsEnabled = expand.IsEnabled = _foldingManager != null;
        };
        textEditor.ContextMenu = menu;
        MenuItem AddAction(string label, string shortcut, Action action)
        {
            var item = new MenuItem { Header = label, InputGestureText = shortcut };
            item.Click += (_, _) => { action(); if (GoToPanel.Visibility != Visibility.Visible && SearchBar.Visibility != Visibility.Visible) textEditor.Focus(); };
            menu.Items.Add(item);
            return item;
        }
    }

    private async void SetFoldings(bool collapsed)
    {
        // An automatic update may already be running; explicitly request the current document.
        _foldingPending = true;
        await UpdateFoldingsAsync();
        if (_foldingManager != null)
            foreach (var folding in _foldingManager.AllFoldings) folding.IsFolded = collapsed;
    }
}
