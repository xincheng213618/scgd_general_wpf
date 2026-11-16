using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Solution.Terminal
{
    internal sealed partial class TerminalView : FrameworkElement, IScrollInfo
    {
        internal const double FontSize = 14;

        private static readonly FontFamily TerminalFont = new("Cascadia Mono, Consolas, Courier New");
        private static readonly Typeface[] Typefaces =
        [
            new(TerminalFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            new(TerminalFont, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            new(TerminalFont, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal),
            new(TerminalFont, FontStyles.Italic, FontWeights.Bold, FontStretches.Normal)
        ];

        private static readonly Color[] LightAnsiForeground = MakePalette(
            0x20242B, 0xA12622, 0x21633E, 0x735000, 0x17519D, 0x85257E, 0x006272, 0x4D545C,
            0x616B76, 0xB4212C, 0x196C42, 0x795300, 0x075CB4, 0x933387, 0x006A79, 0x303640);

        private static readonly Color[] DarkAnsiForeground = MakePalette(
            0x838B97, 0xF07070, 0x53CF94, 0xE3C66E, 0x75AAFF, 0xD88DD3, 0x61C8DD, 0xCDD3DA,
            0x9BA4B0, 0xFF9B9B, 0x83EBB5, 0xFFE785, 0xA0C6FF, 0xF1A8EB, 0x97E3F1, 0xF3F5F7);

        private static readonly GlyphTypeface?[] GlyphTypefaces = Typefaces.Select(typeface =>
        {
            typeface.TryGetGlyphTypeface(out var glyphTypeface);
            return glyphTypeface;
        }).ToArray();

        private static readonly Regex UrlRegex = CreateUrlRegex();

        private readonly TerminalHistoryLayout _lines = new();
        private IReadOnlyList<TerminalLine> _sourceLines = Array.Empty<TerminalLine>();
        private ConditionalWeakTable<TerminalLine, DrawingGroup> _drawings = new();
        private readonly ConditionalWeakTable<TerminalLine, Link[]> _links = new();
        private readonly Dictionary<Color, SolidColorBrush> _brushes = new();
        private int _cursorLine;
        private int _cursorCol;
        private bool _cursorVisible;
        private bool _inputFocused;
        private bool _alternateScreen;
        private int _scrollbackCount;
        private int _columns;
        private long _discardedLines;
        private double _pixelsPerDip;
        private bool _followingOutput = true;
        private (int Line, int Column) _selectionStart;
        private (int Line, int Column) _selectionEnd;
        private bool _hasSelectionAnchor;
        private bool _selecting;
        private Point _selectionPointer;
        private readonly DispatcherTimer _selectionScroll = new(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };

        public static readonly DependencyProperty DefaultBackgroundColorProperty = DependencyProperty.Register(
            nameof(DefaultBackgroundColor), typeof(Color), typeof(TerminalView),
            new FrameworkPropertyMetadata(SystemColors.WindowColor, FrameworkPropertyMetadataOptions.AffectsRender, OnDrawingResourceChanged));

        public static readonly DependencyProperty DefaultForegroundColorProperty = DependencyProperty.Register(
            nameof(DefaultForegroundColor), typeof(Color), typeof(TerminalView),
            new FrameworkPropertyMetadata(SystemColors.WindowTextColor, FrameworkPropertyMetadataOptions.AffectsRender, OnDrawingResourceChanged));

        public static readonly DependencyProperty CursorBrushProperty = DependencyProperty.Register(
            nameof(CursorBrush), typeof(Brush), typeof(TerminalView),
            new FrameworkPropertyMetadata(SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SelectionBrushProperty = DependencyProperty.Register(
            nameof(SelectionBrush), typeof(Brush), typeof(TerminalView),
            new FrameworkPropertyMetadata(SystemColors.HighlightBrush, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UrlBrushProperty = DependencyProperty.Register(
            nameof(UrlBrush), typeof(Brush), typeof(TerminalView),
            new FrameworkPropertyMetadata(SystemColors.HotTrackBrush, FrameworkPropertyMetadataOptions.AffectsRender));

        public Color DefaultBackgroundColor
        {
            get => (Color)GetValue(DefaultBackgroundColorProperty);
            set => SetValue(DefaultBackgroundColorProperty, value);
        }

        public Color DefaultForegroundColor
        {
            get => (Color)GetValue(DefaultForegroundColorProperty);
            set => SetValue(DefaultForegroundColorProperty, value);
        }

        public Brush CursorBrush
        {
            get => (Brush)GetValue(CursorBrushProperty);
            set => SetValue(CursorBrushProperty, value);
        }

        public Brush SelectionBrush
        {
            get => (Brush)GetValue(SelectionBrushProperty);
            set => SetValue(SelectionBrushProperty, value);
        }

        public Brush UrlBrush
        {
            get => (Brush)GetValue(UrlBrushProperty);
            set => SetValue(UrlBrushProperty, value);
        }

        public double CharWidth { get; private set; }
        public double LineHeight { get; private set; }
        public int CursorLine => _cursorLine;
        public int CursorCol => _cursorCol;
        public double CursorY => _cursorLine * LineHeight;
        public bool HasSelection => _selectionStart != _selectionEnd;

        public event Action<string>? UrlClicked;
        public event Action? FontMetricsChanged;
        public event Action? InputRequested;

        public TerminalView()
        {
            Cursor = Cursors.IBeam;
            ClipToBounds = true;
            UpdateFontMetrics(1);
            _selectionScroll.Tick += SelectionScroll_Tick;
        }

        public void SetInputFocus(bool focused)
        {
            _inputFocused = focused;
            InvalidateVisual();
        }

        public void UpdateContent(IReadOnlyList<TerminalLine> lines, int cursorLine, int cursorCol,
            bool cursorVisible, bool alternateScreen, long discardedLines, int scrollbackCount, int columns)
        {
            double retainedOffset = VerticalOffset;
            if (_alternateScreen != alternateScreen)
                ResetViewport();

            int evicted = (int)Math.Clamp(discardedLines - _discardedLines, 0, int.MaxValue);
            bool layoutChanged = evicted != 0 || scrollbackCount != _scrollbackCount || columns != _columns;
            int topLine = (int)(VerticalOffset / LineHeight);
            var anchor = !_followingOutput && layoutChanged ? _lines.GetSourcePosition(topLine) : default;
            var start = layoutChanged && _hasSelectionAnchor ? _lines.GetSourcePosition(_selectionStart.Line, _selectionStart.Column) : default;
            var end = layoutChanged && _hasSelectionAnchor ? _lines.GetSourcePosition(_selectionEnd.Line, _selectionEnd.Column) : default;
            if (columns != _columns && _hasSelectionAnchor && (start.Row >= _scrollbackCount || end.Row >= _scrollbackCount))
                ClearSelection();

            _lines.Update(lines, scrollbackCount, columns, discardedLines);
            _sourceLines = lines;
            if (!_followingOutput && layoutChanged)
            {
                int newTop = _lines.GetDisplayPosition(Math.Max(0, anchor.Row - evicted), anchor.Row < evicted ? 0 : anchor.Column).Line;
                retainedOffset = newTop * LineHeight + (VerticalOffset - topLine * LineHeight);
            }
            if (layoutChanged && _hasSelectionAnchor)
            {
                _selectionStart = RelocateSelection(start, evicted);
                _selectionEnd = RelocateSelection(end, evicted);
            }
            (_cursorLine, _cursorCol) = _lines.GetDisplayPosition(cursorLine, cursorCol);
            _cursorVisible = cursorVisible;
            _alternateScreen = alternateScreen;
            _columns = columns;
            _scrollbackCount = scrollbackCount;
            _discardedLines = discardedLines;
            UpdateScrollExtent(ViewportWidth, ViewportHeight, retainedOffset);
            InvalidateVisual();
        }

        private (int Line, int Column) RelocateSelection((int Row, int Column) position, int evicted) =>
            position.Row < evicted ? (0, 0) : _lines.GetDisplayPosition(position.Row - evicted, position.Column);

        public void ResetViewport()
        {
            ClearSelection();
            _followingOutput = true;
            _discardedLines = 0;
        }

        public void FollowOutput()
        {
            ClearSelection();
            _followingOutput = true;
            SetOffset(MaximumOffset);
        }

        public string GetSelectedText()
        {
            if (!HasSelection)
                return string.Empty;

            var (start, end) = SelectionRange();
            var text = new StringBuilder();
            for (int index = start.Line; index <= end.Line && index < _lines.Count; index++)
            {
                var line = _lines[index];
                int from = index == start.Line ? start.Column : 0;
                int to = index == end.Line ? end.Column : line.Length;
                line.AppendText(text, from, to);
                if (index < end.Line && !line.IsWrapped)
                    text.AppendLine();
            }
            return text.ToString();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            UpdateFontMetrics(newDpi.PixelsPerDip);
            InvalidateMeasure();
            FontMetricsChanged?.Invoke();
        }

        private void UpdateFontMetrics(double pixelsPerDip)
        {
            double topLine = LineHeight > 0 ? VerticalOffset / LineHeight : 0;
            _pixelsPerDip = pixelsPerDip;
            var text = FormatText("M", Typefaces[0], Brushes.Black);
            CharWidth = text.WidthIncludingTrailingWhitespace;
            LineHeight = Math.Ceiling((text.Height + 1) * pixelsPerDip) / pixelsPerDip;
            VerticalOffset = topLine * LineHeight;
            _drawings = new();
        }

        private static void OnDrawingResourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var view = (TerminalView)sender;
            view._drawings = new();
            view._brushes.Clear();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
            double height = double.IsFinite(availableSize.Height) ? availableSize.Height : 0;
            UpdateScrollExtent(width, height);
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            UpdateScrollExtent(finalSize.Width, finalSize.Height);
            return finalSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(GetBrush(DefaultBackgroundColor), null, new Rect(RenderSize));
            int first = Math.Max(0, (int)(VerticalOffset / LineHeight));
            int end = Math.Min(_lines.Count, (int)Math.Ceiling((VerticalOffset + RenderSize.Height) / LineHeight));

            for (int index = first; index < end; index++)
            {
                var line = _lines[index];
                double y = index * LineHeight - VerticalOffset;
                dc.PushTransform(new TranslateTransform(0, y));
                dc.DrawDrawing(_drawings.GetValue(line, RenderLine));
                var (links, sourceColumn) = GetSourceLinks(index);
                foreach (var link in links)
                {
                    int start = Math.Max(0, link.Start - sourceColumn);
                    int linkEnd = Math.Min(line.Length, link.End - sourceColumn);
                    if (linkEnd > start)
                        DrawUnderline(dc, UrlBrush, start, linkEnd);
                }
                if (HasSelection)
                    DrawSelection(dc, index, line.Length);
                if (_cursorVisible && index == _cursorLine)
                {
                    double x = _cursorCol * CharWidth;
                    var cursor = _inputFocused
                        ? new Rect(x, 1, 2 / _pixelsPerDip, LineHeight - 2)
                        : new Rect(x, LineHeight - 3, CharWidth, 1 / _pixelsPerDip);
                    dc.DrawRectangle(CursorBrush, null, cursor);
                }
                dc.Pop();
            }
        }

        private DrawingGroup RenderLine(TerminalLine line)
        {
            var drawing = new DrawingGroup();
            using var dc = drawing.Open();

            var cells = line.Cells;
            for (int start = 0; start < line.Length;)
            {
                var style = cells[start];
                int end = start + 1;
                while (end < line.Length && (cells[end].IsContinuation ||
                    (cells[end].Fg == style.Fg && cells[end].Bg == style.Bg && cells[end].Flags == style.Flags)))
                    end++;

                Color background = GetColor(style.Bg, DefaultBackgroundColor);
                Color foreground = GetForeground(style.Fg, background);
                if (style.IsInverse)
                    (foreground, background) = (background, foreground);
                if (style.IsDim)
                    foreground = Blend(foreground, background);

                if (background != DefaultBackgroundColor)
                    dc.DrawRectangle(GetBrush(background), null, new Rect(start * CharWidth, 0, (end - start) * CharWidth, LineHeight));

                int font = (style.IsBold ? 1 : 0) | (style.IsItalic ? 2 : 0);
                Brush brush = GetBrush(foreground);
                DrawTextRun(dc, cells[start..end], start, font, brush);
                if (style.IsUnderline)
                    DrawUnderline(dc, brush, start, end);
                start = end;
            }

            dc.Close();
            if (drawing.CanFreeze)
                drawing.Freeze();

            return drawing;
        }

        private void DrawTextRun(DrawingContext dc, ReadOnlySpan<TerminalCell> cells, int start, int font, Brush brush)
        {
            var glyphTypeface = GlyphTypefaces[font];
            int position = 0;
            while (position < cells.Length)
            {
                var cell = cells[position];
                if (cell.IsContinuation || (cell.Char == ' ' && cell.Glyph == null))
                {
                    position++;
                    continue;
                }

                if (glyphTypeface != null && IsSimpleCell(cell) && glyphTypeface.CharacterToGlyphMap.ContainsKey(cell.Char))
                {
                    int end = position + 1;
                    while (end < cells.Length && IsSimpleCell(cells[end]) && glyphTypeface.CharacterToGlyphMap.ContainsKey(cells[end].Char))
                        end++;

                    var indices = new ushort[end - position];
                    var advances = new double[indices.Length];
                    for (int i = 0; i < indices.Length; i++)
                    {
                        indices[i] = glyphTypeface.CharacterToGlyphMap[cells[position + i].Char];
                        advances[i] = CharWidth;
                    }
                    var baseline = new Point((start + position) * CharWidth, glyphTypeface.Baseline * FontSize);
                    var glyphs = new GlyphRun(glyphTypeface, 0, false, FontSize, (float)_pixelsPerDip, indices, baseline,
                        advances, null, null, null, null, null, null);
                    dc.DrawGlyphRun(brush, glyphs);
                    position = end;
                    continue;
                }

                var formatted = FormatText(cell.Glyph ?? cell.Char.ToString(), Typefaces[font], brush);
                double x = (start + position) * CharWidth;
                double width = Math.Max(1, cell.DisplayWidth) * CharWidth;
                bool clip = formatted.WidthIncludingTrailingWhitespace > width;
                if (clip)
                    dc.PushClip(new RectangleGeometry(new Rect(x, 0, width, LineHeight)));
                dc.DrawText(formatted, new Point(x, 0));
                if (clip)
                    dc.Pop();
                position++;
            }
        }

        private static bool IsSimpleCell(TerminalCell cell) => cell.Glyph == null && cell.Char is >= ' ' and <= '~';

        private FormattedText FormatText(string text, Typeface typeface, Brush brush) =>
            new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, FontSize, brush, _pixelsPerDip);

        private void DrawUnderline(DrawingContext dc, Brush brush, int start, int end)
        {
            double thickness = 1 / _pixelsPerDip;
            dc.DrawRectangle(brush, null, new Rect(start * CharWidth, LineHeight - 2 * thickness, (end - start) * CharWidth, thickness));
        }

        private Color GetForeground(int code, Color background)
        {
            bool darkBackground = (0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B) < 128;
            if (code is >= 1 and <= 16)
                return (darkBackground ? DarkAnsiForeground : LightAnsiForeground)[code - 1];

            return GetColor(code, DefaultForegroundColor);
        }

        private static Color GetColor(int code, Color fallback)
        {
            if (code == 0)
                return fallback;
            if (code >= 0x1000000)
                return Rgb(code);
            var color = TerminalScreenBuffer.Get256Color(code - 1);
            return Color.FromRgb(color.R, color.G, color.B);
        }

        private SolidColorBrush GetBrush(Color color)
        {
            if (_brushes.TryGetValue(color, out var brush))
                return brush;

            brush = new SolidColorBrush(color);
            brush.Freeze();
            if (_brushes.Count >= 256)
                _brushes.Clear();
            _brushes.Add(color, brush);
            return brush;
        }

        private static Color[] MakePalette(params int[] colors) => Array.ConvertAll(colors, Rgb);
        private static Color Rgb(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        private static Color Blend(Color a, Color b) => Color.FromRgb((byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2));

        private void DrawSelection(DrawingContext dc, int lineIndex, int lineLength)
        {
            var (start, end) = SelectionRange();
            if (lineIndex < start.Line || lineIndex > end.Line)
                return;

            int from = lineIndex == start.Line ? start.Column : 0;
            int to = lineIndex == end.Line ? end.Column : Math.Max(lineLength, 1);
            if (to > from)
                dc.DrawRectangle(SelectionBrush, null, new Rect(from * CharWidth, 0, (to - from) * CharWidth, LineHeight));
        }

        private ((int Line, int Column) Start, (int Line, int Column) End) SelectionRange() =>
            _selectionStart.CompareTo(_selectionEnd) <= 0 ? (_selectionStart, _selectionEnd) : (_selectionEnd, _selectionStart);

        private void ClearSelection()
        {
            bool hadSelection = HasSelection;
            _selectionStart = _selectionEnd = default;
            _hasSelectionAnchor = false;
            if (_selecting)
            {
                EndSelectionDrag();
                ReleaseMouseCapture();
            }
            if (hadSelection)
                InvalidateVisual();
        }

        private (int Line, int Column) HitTestCell(Point point, bool nearestEdge)
        {
            if (_lines.Count == 0)
                return default;

            int index = Math.Clamp((int)Math.Floor((point.Y + VerticalOffset) / LineHeight), 0, _lines.Count - 1);
            var line = _lines[index];
            double column = Math.Max(0, point.X / CharWidth);
            int cell = Math.Min((int)column, line.Length);
            if (cell == line.Length)
                return (index, cell);

            while (cell > 0 && line.Cells[cell].IsContinuation)
                cell--;
            int width = Math.Max(1, line.Cells[cell].DisplayWidth);
            return (index, nearestEdge && column - cell >= width / 2.0 ? Math.Min(line.Length, cell + width) : cell);
        }

        internal void SelectWordAt(Point point)
        {
            var (index, column) = HitTestCell(point, nearestEdge: false);
            _hasSelectionAnchor = true;
            _selectionStart = _selectionEnd = (index, column);
            if (_lines.Count == 0 || column == _lines[index].Length)
            {
                InvalidateVisual();
                return;
            }

            var cells = _lines[index].Cells;
            bool blank = char.IsWhiteSpace(cells[column].Char);
            int start = column;
            int end = column + cells[column].DisplayWidth;
            while (start > 0)
            {
                int previous = cells[start - 1].IsContinuation ? start - 2 : start - 1;
                if (char.IsWhiteSpace(cells[previous].Char) != blank)
                    break;
                start = previous;
            }
            while (end < cells.Length && char.IsWhiteSpace(cells[end].Char) == blank)
                end += cells[end].DisplayWidth;

            _selectionStart = (index, start);
            _selectionEnd = (index, end);
            _followingOutput = false;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var point = e.GetPosition(this);
            if (Keyboard.Modifiers == ModifierKeys.Control && FindLink(point) is string url)
            {
                UrlClicked?.Invoke(url);
                e.Handled = true;
                return;
            }

            if (e.ClickCount == 2 && Keyboard.Modifiers == ModifierKeys.None)
            {
                SelectWordAt(point);
            }
            else
            {
                var cell = HitTestCell(point, nearestEdge: true);
                if (!_hasSelectionAnchor || !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    _selectionStart = cell;
                _hasSelectionAnchor = true;
                _selectionEnd = cell;
                if (HasSelection)
                    _followingOutput = false;
                _selecting = CaptureMouse();
            }
            InputRequested?.Invoke();
            e.Handled = true;
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var point = e.GetPosition(this);
            Cursor = !_selecting && Keyboard.Modifiers == ModifierKeys.Control && FindLink(point) != null
                ? Cursors.Hand : Cursors.IBeam;
            if (!_selecting)
                return;

            _selectionPointer = point;
            if (point.Y < 0 || point.Y >= ViewportHeight)
            {
                if (!_selectionScroll.IsEnabled)
                {
                    SelectionScroll_Tick(null, EventArgs.Empty);
                    _selectionScroll.Start();
                }
            }
            else
            {
                _selectionScroll.Stop();
                UpdateSelectionEnd(point);
            }
        }

        private void SelectionScroll_Tick(object? sender, EventArgs e)
        {
            double edge = _selectionPointer.Y < 0 ? 0 : Math.Max(0, ViewportHeight - 1 / _pixelsPerDip);
            double distance = _selectionPointer.Y - edge;
            double rows = Math.Clamp(Math.Ceiling(Math.Abs(distance) / LineHeight), 1, 8);
            SetVerticalOffset(VerticalOffset + Math.CopySign(rows * LineHeight, distance));
            UpdateSelectionEnd(new Point(_selectionPointer.X, edge));
            if (HasSelection)
                _followingOutput = false;
        }

        private void UpdateSelectionEnd(Point point)
        {
            var cell = HitTestCell(point, nearestEdge: true);
            if (cell != _selectionEnd)
            {
                _selectionEnd = cell;
                if (HasSelection)
                    _followingOutput = false;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (IsMouseCaptured)
                ReleaseMouseCapture();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            EndSelectionDrag();
        }

        private void EndSelectionDrag()
        {
            _selecting = false;
            _selectionScroll.Stop();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollWheel(e.Delta / (double)Mouse.MouseWheelDeltaForOneLine);
            e.Handled = true;
        }

        private string? FindLink(Point point)
        {
            if (_lines.Count == 0 || point.X < 0 || point.Y < 0 || point.Y >= ViewportHeight)
                return null;

            int index = (int)((point.Y + VerticalOffset) / LineHeight);
            if (index >= _lines.Count)
                return null;

            int column = (int)(point.X / CharWidth);
            if (column >= _lines[index].Length)
                return null;

            var (links, sourceColumn) = GetSourceLinks(index);
            foreach (var link in links)
            {
                if (column + sourceColumn >= link.Start && column + sourceColumn < link.End)
                    return link.Url;
            }
            return null;
        }

        private (Link[] Links, int SourceColumn) GetSourceLinks(int displayLine)
        {
            var (row, column) = _lines.GetSourcePosition(displayLine);
            return (_links.GetValue(_sourceLines[row], FindLinks), column);
        }

        private static Link[] FindLinks(TerminalLine line)
        {
            var text = line.Text;
            if (!text.Contains("http", StringComparison.OrdinalIgnoreCase))
                return [];

            return UrlRegex.Matches(text).Select(match => new Link(
                line.GetColumnForTextOffset(match.Index),
                line.GetColumnForTextOffset(match.Index + match.Length, roundUp: true),
                match.Value)).ToArray();
        }

        public ScrollViewer? ScrollOwner { get; set; }
        public bool CanVerticallyScroll { get; set; }
        public bool CanHorizontallyScroll { get; set; }
        public double ExtentWidth => ViewportWidth;
        public double ExtentHeight { get; private set; }
        public double ViewportWidth { get; private set; }
        public double ViewportHeight { get; private set; }
        public double HorizontalOffset => 0;
        public double VerticalOffset { get; private set; }
        private double MaximumOffset => Math.Max(0, ExtentHeight - ViewportHeight);
        private double PageHeight(double viewportHeight) => Math.Max(1, Math.Floor(viewportHeight / LineHeight)) * LineHeight;

        private void UpdateScrollExtent(double width, double height, double? retainedOffset = null)
        {
            // Reserve the partial row below the live screen instead of exposing a clipped
            // scrollback row above the current page when following output.
            double extent = height + Math.Max(0, _lines.Count * LineHeight - PageHeight(height));
            bool changed = ViewportWidth != width || ViewportHeight != height || ExtentHeight != extent;
            ViewportWidth = width;
            ViewportHeight = height;
            ExtentHeight = extent;
            if (!SetOffset(_followingOutput ? MaximumOffset : retainedOffset ?? VerticalOffset) && changed)
                ScrollOwner?.InvalidateScrollInfo();
        }

        private bool SetOffset(double offset)
        {
            double next = Math.Clamp(offset, 0, MaximumOffset);
            if (VerticalOffset == next)
                return false;
            VerticalOffset = next;
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateVisual();
            return true;
        }

        public void SetVerticalOffset(double offset)
        {
            SetOffset(offset);
            _followingOutput = VerticalOffset >= MaximumOffset;
        }

        public void LineUp() => SetVerticalOffset(VerticalOffset - LineHeight);
        public void LineDown() => SetVerticalOffset(VerticalOffset + LineHeight);
        public void PageUp() => SetVerticalOffset(VerticalOffset - PageHeight(ViewportHeight));
        public void PageDown() => SetVerticalOffset(VerticalOffset + PageHeight(ViewportHeight));
        public void MouseWheelUp() => ScrollWheel(1);
        public void MouseWheelDown() => ScrollWheel(-1);

        private void ScrollWheel(double steps)
        {
            int lines = SystemParameters.WheelScrollLines;
            double distance = lines < 0 ? PageHeight(ViewportHeight) : lines * LineHeight;
            if (distance > 0)
                SetVerticalOffset(VerticalOffset - steps * distance);
        }
        public void LineLeft() { }
        public void LineRight() { }
        public void PageLeft() { }
        public void PageRight() { }
        public void MouseWheelLeft() { }
        public void MouseWheelRight() { }
        public void SetHorizontalOffset(double offset) { }
        public Rect MakeVisible(Visual visual, Rect rectangle) => Rect.Empty;

        private readonly record struct Link(int Start, int End, string Url);

        [GeneratedRegex(@"https?://[^\s<>""'\]）》]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex CreateUrlRegex();
    }
}
