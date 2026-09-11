using ICSharpCode.AvalonEdit.Highlighting;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AvalonTextEditor = ICSharpCode.AvalonEdit.TextEditor;

namespace ColorVision.Solution.Editor.AvalonEditor;

/// <summary>A document overview. Scrolling only repaints the viewport; text is rendered in bounded dispatcher batches.</summary>
public sealed class EditorMinimap : FrameworkElement, IDisposable
{
    private AvalonTextEditor? _editor;
    private Func<IHighlightingDefinition?>? _getHighlighting;
    private DrawingGroup? _drawing;
    private DispatcherOperation? _renderOperation;
    private readonly DispatcherTimer _hoverTimer;
    private Popup? _preview;
    private AvalonTextEditor? _previewEditor;
    private TextBlock? _previewTitle;
    private AvalonEditorThemeController? _previewTheme;
    private DocumentHighlighter? _highlighter;
    private bool _attached;
    private bool _disposed;
    private int _generation;
    private double _dragAnchor;
    private double _hoverY;
    private int[] _searchLines = [];
    private int? _errorLine;
    internal int? ErrorLine { get => _errorLine; set { _errorLine = value; InvalidateVisual(); } }
    internal void SetSearchLines(IEnumerable<int> lines) { _searchLines = lines.Distinct().Order().ToArray(); InvalidateVisual(); }
    private double RowHeight => _editor?.Document is { } document ? Math.Min(2.8, ActualHeight / Math.Max(1, document.LineCount)) : 0;
    public bool ShowPreview { get; set; } = true;
    internal bool BoldKeywords { get; set; }
    internal int RenderCount { get; private set; }
    internal Rect ViewportRectangle
    {
        get
        {
            if (_editor?.Document == null || RowHeight <= 0) return Rect.Empty;
            double top = DocumentPosition(_editor.VerticalOffset) * RowHeight;
            double bottom = DocumentPosition(_editor.VerticalOffset + _editor.ViewportHeight) * RowHeight;
            return new Rect(0, Math.Max(0, top), ActualWidth, Math.Max(4, Math.Min(ActualHeight, bottom) - top));
        }
    }

    public EditorMinimap()
    {
        ClipToBounds = true;
        Focusable = false;
        Cursor = Cursors.Arrow;
        System.Windows.Automation.AutomationProperties.SetName(this, "代码地图");
        _hoverTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(350) };
        _hoverTimer.Tick += HoverTimer_Tick;
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) => { if (IsVisible) Attach(); else Detach(); };
        SizeChanged += (_, _) => InvalidateDocument();
    }

    internal void Initialize(AvalonTextEditor editor, Func<IHighlightingDefinition?> getHighlighting)
    {
        Detach();
        _editor = editor;
        _getHighlighting = getHighlighting;
        if (IsLoaded && IsVisible) Attach();
    }

    private void Attach()
    {
        if (_disposed || _attached || _editor == null || !IsLoaded || !IsVisible) return;
        _attached = true;
        _editor.TextChanged += SourceChanged;
        _editor.TextArea.TextView.ScrollOffsetChanged += ViewChanged;
        _editor.TextArea.TextView.VisualLinesChanged += ViewChanged;
        _editor.TextArea.Caret.PositionChanged += ViewChanged;
        InvalidateDocument();
    }

    private void Detach()
    {
        CancelRender();
        HidePreview();
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (!_attached || _editor == null) return;
        _attached = false;
        _editor.TextChanged -= SourceChanged;
        _editor.TextArea.TextView.ScrollOffsetChanged -= ViewChanged;
        _editor.TextArea.TextView.VisualLinesChanged -= ViewChanged;
        _editor.TextArea.Caret.PositionChanged -= ViewChanged;
        _drawing = null;
    }

    private void SourceChanged(object? sender, EventArgs e) => InvalidateDocument();
    private void ViewChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void CancelRender()
    {
        ++_generation;
        _renderOperation?.Abort();
        _renderOperation = null;
        _highlighter?.Dispose();
        _highlighter = null;
    }

    internal void InvalidateDocument()
    {
        CancelRender();
        HidePreview();
        _drawing = null;
        InvalidateVisual();
        if (!_attached || _editor?.Document == null || ActualWidth <= 0 || RowHeight <= 0) return;
        int generation = _generation;
        // Coalesce a burst of edits before doing any text layout.
        _renderOperation = Dispatcher.BeginInvoke(DispatcherPriority.Background, () => BeginRender(generation));
    }

    private void BeginRender(int generation)
    {
        if (generation != _generation || _editor?.Document == null) return;
        var document = _editor.Document;
        var drawing = new DrawingGroup();
        var definition = _getHighlighting?.Invoke();
        // Very large documents retain navigation and structure without a second full syntax pass.
        if (definition != null && document.TextLength <= 2_000_000)
            _highlighter = new DocumentHighlighter(document, definition);
        int samples = Math.Min(document.LineCount, Math.Max(1, (int)Math.Ceiling(ActualHeight * VisualTreeHelper.GetDpi(this).DpiScaleY)));
        int sample = 0;
        double rowHeight = RowHeight;
        var typeface = new Typeface(_editor.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        Brush foreground = FindBrush("EditorForegroundBrush", Brushes.Gray);
        void RenderBatch()
        {
            if (generation != _generation) return;
            var watch = Stopwatch.StartNew();
            using (var dc = drawing.Append())
            {
                while (sample < samples && watch.ElapsedMilliseconds < 6)
                {
                    int lineNumber = 1 + (int)((long)sample * document.LineCount / samples);
                    var line = document.GetLineByNumber(lineNumber);
                    string text = document.GetText(line.Offset, Math.Min(line.Length, 180));
                    // Keep tabs as tab stops, rather than replacing each tab with an arbitrary fixed number of spaces.
                    var expanded = new System.Text.StringBuilder();
                    var columns = new int[text.Length + 1];
                    for (int i = 0; i < text.Length; i++)
                    {
                        columns[i] = expanded.Length;
                        if (text[i] == '\t') expanded.Append(' ', _editor.Options.IndentationSize - expanded.Length % _editor.Options.IndentationSize);
                        else expanded.Append(text[i]);
                    }
                    columns[text.Length] = expanded.Length;
                    if (expanded.Length > 0)
                    {
                        var formatted = new FormattedText(expanded.ToString(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                            typeface, 3.5, foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        if (_highlighter != null)
                        {
                            foreach (var section in _highlighter.HighlightLine(lineNumber).Sections)
                            {
                                int start = Math.Clamp(section.Offset - line.Offset, 0, text.Length);
                                int end = Math.Clamp(section.Offset + section.Length - line.Offset, start, text.Length);
                                string? key = ThemeAwareHighlightingColorizer.GetForegroundBrushResourceKey(section.Color);
                                if (end > start && key != null) formatted.SetForegroundBrush(FindBrush(key, foreground), columns[start], columns[end] - columns[start]);
                            }
                        }
                        dc.PushTransform(new TranslateTransform(5, (lineNumber - 1) * rowHeight));
                        dc.PushTransform(new ScaleTransform(1, Math.Min(1, Math.Max(rowHeight, ActualHeight / samples) / formatted.Height)));
                        dc.DrawText(formatted, new Point());
                        dc.Pop();
                        dc.Pop();
                    }
                    sample++;
                }
            }
            if (sample < samples)
                _renderOperation = Dispatcher.BeginInvoke(DispatcherPriority.Background, RenderBatch);
            else
            {
                _highlighter?.Dispose();
                _highlighter = null;
                _renderOperation = null;
                if (drawing.CanFreeze) drawing.Freeze();
                _drawing = drawing;
                RenderCount++;
                InvalidateVisual();
            }
        }
        RenderBatch();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dc = drawingContext;
        dc.DrawRectangle(FindBrush("EditorOverviewBackgroundBrush", Brushes.Transparent), null, new Rect(RenderSize));
        if (_drawing != null) dc.DrawDrawing(_drawing);
        Rect viewport = ViewportRectangle;
        if (!viewport.IsEmpty)
            dc.DrawRectangle(FindBrush("EditorOverviewViewportBrush", Brushes.Transparent), new Pen(FindBrush("EditorOverviewBorderBrush", Brushes.Gray), 1), viewport);
        if (_editor?.Document != null && RowHeight > 0)
        {
            double previous = -10;
            var matchBrush = FindBrush("EditorSearchMarkerBrush", Brushes.Goldenrod);
            foreach (int line in _searchLines)
            {
                double markerY = Math.Floor((line - 1) * RowHeight);
                if (markerY - previous < 2) continue;
                dc.DrawRectangle(matchBrush, null, new Rect(Math.Max(0, ActualWidth - 6), markerY, 5, 2));
                previous = markerY;
            }
            if (ErrorLine is int errorLine)
                dc.DrawRectangle(FindBrush("EditorSyntaxErrorBrush", Brushes.Red), null, new Rect(0, (errorLine - 1) * RowHeight, ActualWidth, 2));
            double y = (_editor.TextArea.Caret.Line - 1) * RowHeight;
            dc.DrawLine(new Pen(FindBrush("EditorCaretBrush", Brushes.Gray), 1), new Point(0, y), new Point(ActualWidth, y));
        }
    }

    private Brush FindBrush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private double DocumentPosition(double visualY)
    {
        var view = _editor!.TextArea.TextView;
        if (visualY >= view.DocumentHeight) return _editor.Document.LineCount;
        var line = view.GetDocumentLineByVisualTop(Math.Max(0, visualY));
        double top = view.GetVisualTopByDocumentLine(line.LineNumber);
        double bottom = line.NextLine != null ? view.GetVisualTopByDocumentLine(line.NextLine.LineNumber) : view.DocumentHeight;
        return line.LineNumber - 1 + Math.Clamp((visualY - top) / Math.Max(view.DefaultLineHeight, bottom - top), 0, 1);
    }

    internal void ScrollToMapPosition(double y, bool center)
    {
        if (_editor?.Document == null || RowHeight <= 0) return;
        double position = Math.Clamp(y / RowHeight - (center ? 0 : _dragAnchor), 0, _editor.Document.LineCount - 1);
        int line = (int)position + 1;
        var view = _editor.TextArea.TextView;
        var visualLine = view.GetOrConstructVisualLine(_editor.Document.GetLineByNumber(line));
        double viewportAnchor = (center ? _editor.ViewportHeight / 2 : 0) - (position - Math.Floor(position)) * visualLine.Height;
        // AvalonEdit measures the wrapped lines preceding this anchor before it calculates the offset.
        // A raw ScrollToVerticalOffset would use estimated heights and land on the wrong document line.
        _editor.ScrollTo(line, -1, ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineTop, viewportAnchor, 0);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_editor?.Document == null || RowHeight <= 0) return;
        HidePreview();
        double y = e.GetPosition(this).Y;
        Rect viewport = ViewportRectangle;
        if (!viewport.Contains(e.GetPosition(this))) ScrollToMapPosition(y, center: true);
        _editor.UpdateLayout();
        _dragAnchor = y / RowHeight - DocumentPosition(_editor.VerticalOffset);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _hoverY = e.GetPosition(this).Y;
        if (IsMouseCaptured) ScrollToMapPosition(_hoverY, center: false);
        else if (ShowPreview)
        {
            _hoverTimer.Stop();
            _hoverTimer.Start();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured) ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); HidePreview(); }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (_editor == null) return;
        _editor.ScrollToVerticalOffset(_editor.VerticalOffset - e.Delta / 120.0 * _editor.TextArea.TextView.DefaultLineHeight * 3);
        e.Handled = true;
    }

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (!ShowPreview || !IsMouseOver || IsMouseCaptured || _editor?.Document == null) return;
        ShowCodePreview(_hoverY);
    }

    internal void ShowCodePreview(double y)
    {
        if (_editor?.Document == null || RowHeight <= 0) return;
        int line = Math.Clamp((int)(y / RowHeight) + 1, 1, _editor.Document.LineCount);
        if (_preview == null)
        {
            _previewEditor = new AvalonTextEditor { IsReadOnly = true, IsHitTestVisible = false, Focusable = false,
                ShowLineNumbers = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
            _previewEditor.SetResourceReference(AvalonTextEditor.BackgroundProperty, "EditorBackgroundBrush");
            _previewEditor.SetResourceReference(AvalonTextEditor.ForegroundProperty, "EditorForegroundBrush");
            _previewEditor.SetResourceReference(AvalonTextEditor.LineNumbersForegroundProperty, "EditorLineNumberBrush");
            _previewTheme = new AvalonEditorThemeController(_previewEditor);
            _previewEditor.Options.HighlightCurrentLine = false;
            _previewTitle = new TextBlock { Margin = new Thickness(10, 7, 10, 7), FontSize = 12 };
            _previewTitle.SetResourceReference(TextBlock.ForegroundProperty, "EditorLineNumberBrush");
            var panel = new DockPanel();
            DockPanel.SetDock(_previewTitle, Dock.Top);
            panel.Children.Add(_previewTitle);
            panel.Children.Add(_previewEditor);
            var border = new Border { Child = panel, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(4), Width = 600, Height = 206 };
            border.SetResourceReference(Border.BackgroundProperty, "EditorBackgroundBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "EditorOverviewBorderBrush");
            _preview = new Popup { Child = border, PlacementTarget = this, Placement = PlacementMode.Left, StaysOpen = true, AllowsTransparency = true, IsHitTestVisible = false };
        }
        _previewEditor!.Document = _editor.Document;
        _previewEditor.FontFamily = _editor.FontFamily;
        _previewEditor.FontSize = Math.Min(15, _editor.FontSize);
        _previewEditor.Options.IndentationSize = _editor.Options.IndentationSize;
        _previewTheme!.BoldKeywords = BoldKeywords;
        _previewTheme.SetHighlighting(_getHighlighting?.Invoke());
        _previewTheme.RefreshTheme();
        _previewTitle!.Text = $"行 {line:N0} / {_editor.Document.LineCount:N0}";
        if (_preview.Child is FrameworkElement child) child.Width = Math.Clamp(_editor.ActualWidth - 24, 280, 650);
        _preview.VerticalOffset = Math.Clamp(y - 100, 0, Math.Max(0, ActualHeight - 206));
        _preview.IsOpen = true;
        _previewEditor.UpdateLayout();
        _previewEditor.ScrollToVerticalOffset(_previewEditor.TextArea.TextView.GetVisualTopByDocumentLine(Math.Max(1, line - 4)));
    }

    internal void HidePreview()
    {
        _hoverTimer.Stop();
        if (_preview != null) _preview.IsOpen = false;
        if (_previewEditor != null) _previewEditor.Document = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        _hoverTimer.Tick -= HoverTimer_Tick;
        _previewTheme?.Dispose();
        _preview = null;
        _previewEditor = null;
        _editor = null;
        _getHighlighting = null;
    }
}
