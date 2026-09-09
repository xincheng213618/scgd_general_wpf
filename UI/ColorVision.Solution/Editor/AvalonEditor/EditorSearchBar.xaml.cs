using ICSharpCode.AvalonEdit.Rendering;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AvalonTextEditor = ICSharpCode.AvalonEdit.TextEditor;

namespace ColorVision.Solution.Editor.AvalonEditor;

public partial class EditorSearchBar : UserControl, IDisposable, IBackgroundRenderer
{
    private AvalonTextEditor? _editor;
    private readonly DispatcherTimer _timer;
    private IReadOnlyList<Match> _matches = [];
    private string? _error;
    private bool _stale = true;
    private int _index = -1;
    private bool _replacing;
    public KnownLayer Layer => KnownLayer.Selection;
    internal IReadOnlyList<Match> Matches => _matches;
    internal event EventHandler? MatchesChanged;

    public EditorSearchBar()
    {
        InitializeComponent();
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(160) };
        _timer.Tick += Timer_Tick;
        Unloaded += (_, _) => _timer.Stop();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { _stale = true; RefreshMatches(); }
            else _timer.Stop();
        };
    }

    internal void Initialize(AvalonTextEditor editor)
    {
        _editor = editor;
        editor.TextChanged += DocumentChanged;
        editor.TextArea.TextView.BackgroundRenderers.Add(this);
    }

    internal void Open(bool replace)
    {
        Visibility = Visibility.Visible;
        ReplacementBox.Visibility = ReplaceActions.Visibility = replace ? Visibility.Visible : Visibility.Collapsed;
        if (_editor!.SelectionLength is > 0 and < 500 && !_editor.SelectedText.Contains('\n')) QueryBox.Text = _editor.SelectedText;
        _stale = true;
        RefreshMatches();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!IsVisible) return;
            Keyboard.Focus(QueryBox);
            QueryBox.SelectAll();
        });
    }

    internal void Close()
    {
        _timer.Stop();
        Visibility = Visibility.Collapsed;
        _matches = [];
        _stale = true;
        MatchesChanged?.Invoke(this, EventArgs.Empty);
        _editor?.TextArea.TextView.InvalidateLayer(Layer);
        _editor?.Focus();
    }

    private void Timer_Tick(object? sender, EventArgs e) { _timer.Stop(); RefreshMatches(); }
    private void Query_Changed(object sender, TextChangedEventArgs e) => ScheduleSearch();
    private void Options_Changed(object sender, RoutedEventArgs e) => ScheduleSearch();
    private void DocumentChanged(object? sender, EventArgs e) { if (!_replacing) ScheduleSearch(); }
    private void ScheduleSearch()
    {
        _stale = true;
        _matches = [];
        MatchesChanged?.Invoke(this, EventArgs.Empty);
        _editor?.TextArea.TextView.InvalidateLayer(Layer);
        if (_timer == null || !IsVisible) return;
        _timer.Stop();
        _timer.Start();
    }

    internal static IReadOnlyList<Match> FindMatches(string text, string query, bool matchCase, bool wholeWord, bool regex)
    {
        if (query.Length == 0) return [];
        string pattern = regex ? query : Regex.Escape(query);
        if (wholeWord) pattern = @"(?<!\w)(?:" + pattern + @")(?!\w)";
        var expression = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase), TimeSpan.FromMilliseconds(150));
        var result = new List<Match>();
        foreach (Match match in expression.Matches(text))
        {
            if (result.Count == 20_000) throw new InvalidOperationException("匹配超过 20,000 处，请缩小查找范围。");
            result.Add(match);
        }
        return result;
    }

    private void RefreshMatches()
    {
        if (_editor == null || !_stale) return;
        _timer.Stop();
        _error = null;
        _index = -1;
        try { _matches = QueryBox.Text.Length == 0 ? [] : FindMatches(_editor.Text, QueryBox.Text, CaseBox.IsChecked == true, WordBox.IsChecked == true, RegexBox.IsChecked == true); }
        catch (ArgumentException) { _matches = []; _error = "正则表达式不完整或无效。"; }
        catch (RegexMatchTimeoutException) { _matches = []; _error = "查找超时，请简化正则表达式。"; }
        catch (InvalidOperationException ex) { _matches = []; _error = ex.Message; }
        _stale = false;
        MatchesChanged?.Invoke(this, EventArgs.Empty);
        UpdateStatus();
        _editor.TextArea.TextView.InvalidateLayer(Layer);
    }

    private void UpdateStatus()
    {
        ResultText.Text = _error ?? (QueryBox.Text.Length == 0 ? "Enter 下一个 · Shift+Enter 上一个 · Esc 关闭" : _matches.Count == 0 ? "未找到匹配项" : $"{(_index >= 0 ? $"{_index + 1:N0} / " : "")}{_matches.Count:N0} 个匹配");
        ReplaceButton.IsEnabled = ReplaceAllButton.IsEnabled = _error == null && _matches.Count > 0 && _editor?.IsReadOnly == false;
    }

    internal void FindNext(bool previous = false)
    {
        if (Visibility != Visibility.Visible) { Open(false); return; }
        RefreshMatches();
        if (_matches.Count == 0) return;
        if (_index < 0)
        {
            int caret = _editor!.CaretOffset;
            _index = previous ? Enumerable.Range(0, _matches.Count).LastOrDefault(i => _matches[i].Index < caret, _matches.Count - 1)
                : Enumerable.Range(0, _matches.Count).FirstOrDefault(i => _matches[i].Index >= caret, 0);
        }
        else _index = (_index + (previous ? -1 : 1) + _matches.Count) % _matches.Count;
        var match = _matches[_index];
        _editor!.Select(match.Index, match.Length);
        _editor.TextArea.Caret.BringCaretToView();
        UpdateStatus();
    }

    internal static string Replacement(Match match, string replacement, bool regex) => regex ? match.Result(replacement) : replacement;

    private void Replace(bool all)
    {
        RefreshMatches();
        if (_editor?.IsReadOnly != false || _error != null || _matches.Count == 0) return;
        if (!all && (_index < 0 || _editor.SelectionStart != _matches[_index].Index || _editor.SelectionLength != _matches[_index].Length)) { FindNext(); return; }
        try
        {
            var replacements = (all ? _matches : [_matches[_index]])
                .Select(m => (m.Index, m.Length, Text: Replacement(m, ReplacementBox.Text, RegexBox.IsChecked == true))).ToArray();
            _replacing = true;
            using (_editor.Document.RunUpdate())
                foreach (var item in replacements.Reverse()) _editor.Document.Replace(item.Index, item.Length, item.Text);
            if (!all)
            {
                var item = replacements[0];
                _editor.CaretOffset = Math.Min(_editor.Document.TextLength, item.Index + item.Text.Length + (item.Length == 0 ? 1 : 0));
            }
            _replacing = false;
            _stale = true;
            RefreshMatches();
            if (!all) FindNext();
            else ResultText.Text = $"已替换 {replacements.Length:N0} 处 · Ctrl+Z 撤销";
        }
        catch (ArgumentException) { _error = "替换表达式无效。"; UpdateStatus(); }
        finally { _replacing = false; }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var view = textView;
        if (!IsVisible || _stale || !view.VisualLinesValid || view.VisualLines.Count == 0) return;
        int first = view.VisualLines[0].FirstDocumentLine.Offset;
        int last = view.VisualLines[^1].LastDocumentLine.EndOffset;
        Brush brush = TryFindResource("EditorSearchMatchBrush") as Brush ?? Brushes.Gold;
        foreach (var match in _matches)
        {
            if (match.Index > last) break;
            if (match.Index + match.Length < first || match.Length == 0) continue;
            foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(view, new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset = match.Index, Length = match.Length }))
                drawingContext.DrawRoundedRectangle(brush, null, rect, 2, 2);
        }
    }

    private void Search_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key == Key.Enter) { FindNext(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); e.Handled = true; }
    }
    private void Previous_Click(object sender, RoutedEventArgs e) => FindNext(true);
    private void Next_Click(object sender, RoutedEventArgs e) => FindNext();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Replace_Click(object sender, RoutedEventArgs e) => Replace(false);
    private void ReplaceAll_Click(object sender, RoutedEventArgs e) => Replace(true);
    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        if (_editor != null)
        {
            _editor.TextChanged -= DocumentChanged;
            _editor.TextArea.TextView.BackgroundRenderers.Remove(this);
        }
        _editor = null;
        _matches = [];
        MatchesChanged = null;
        GC.SuppressFinalize(this);
    }
}
