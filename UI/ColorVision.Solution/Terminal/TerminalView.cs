using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Solution.Terminal
{
    /// <summary>
    /// High-performance custom terminal view using OnRender for colored text rendering.
    /// Supports ANSI colors, cursor display, URL detection with Ctrl+Click, text selection.
    /// Uses MeasureOverride for proper ScrollViewer integration.
    /// </summary>
    internal partial class TerminalView : FrameworkElement
    {
        // Rendering constants
        private const double DefaultFontSize = 14;
        private static readonly Typeface DefaultTypeface = new(new FontFamily("Cascadia Mono, Consolas, Courier New"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface BoldTypeface = new(new FontFamily("Cascadia Mono, Consolas, Courier New"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        // Frozen brushes for performance
        private static readonly Brush DefaultBgBrush;
        private static readonly Brush CursorBrush;
        private static readonly Pen UrlPen;

        private static readonly Color DefaultFgColor = Color.FromRgb(204, 204, 204);
        private static readonly Color DefaultBgColor = Color.FromRgb(30, 30, 30);

        static TerminalView()
        {
            var bg = new SolidColorBrush(DefaultBgColor);
            bg.Freeze();
            DefaultBgBrush = bg;

            var cur = new SolidColorBrush(Color.FromArgb(128, 204, 204, 204));
            cur.Freeze();
            CursorBrush = cur;

            var urlBrush = new SolidColorBrush(Color.FromRgb(88, 166, 255));
            urlBrush.Freeze();
            UrlPen = new Pen(urlBrush, 1);
            UrlPen.Freeze();
        }

        // State
        private List<TerminalLine> _lines = new();
        private int _cursorLine;
        private int _cursorCol;
        private double _charWidth;
        private double _lineHeight;

        // URL detection
        private static readonly Regex UrlRegex = CreateUrlRegex();
        private readonly List<UrlHitRegion> _urlRegions = new();
        private UrlHitRegion? _hoveredUrl;

        // Selection
        private bool _isSelecting;
        private int _selStartLine, _selStartCol;
        private int _selEndLine, _selEndCol;
        private bool _hasSelection;

        public event Action<string>? UrlClicked;

        public TerminalView()
        {
            Focusable = true;
            FocusVisualStyle = null;
            Cursor = Cursors.IBeam;
            ClipToBounds = true;

            var ft = new FormattedText("M", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, DefaultTypeface, DefaultFontSize, Brushes.White, 1.0);
            _charWidth = ft.WidthIncludingTrailingWhitespace;
            _lineHeight = ft.Height;
        }

        public double CharWidth => _charWidth;
        public double LineHeight => _lineHeight;
        public int CursorLine => _cursorLine;
        public int CursorCol => _cursorCol;
        public double CursorY => _cursorLine * _lineHeight;

        /// <summary>
        /// Update the view with new terminal data. Triggers re-measure and re-render.
        /// </summary>
        public void UpdateContent(List<TerminalLine> lines, int cursorLine, int cursorCol)
        {
            _lines = lines;
            _cursorLine = cursorLine;
            _cursorCol = cursorCol;
            InvalidateMeasure();
            InvalidateVisual();
        }

        public string GetSelectedText()
        {
            if (!_hasSelection) return string.Empty;

            int startLine = _selStartLine, startCol = _selStartCol;
            int endLine = _selEndLine, endCol = _selEndCol;

            if (startLine > endLine || (startLine == endLine && startCol > endCol))
            {
                (startLine, endLine) = (endLine, startLine);
                (startCol, endCol) = (endCol, startCol);
            }

            var sb = new System.Text.StringBuilder();
            for (int line = startLine; line <= endLine && line < _lines.Count; line++)
            {
                var tl = _lines[line];
                int from = (line == startLine) ? startCol : 0;
                int to = (line == endLine) ? endCol : tl.Length;
                from = Math.Max(0, Math.Min(from, tl.Length));
                to = Math.Max(from, Math.Min(to, tl.Length));

                for (int c = from; c < to; c++)
                    sb.Append(tl.Cells[c].Char);

                if (line < endLine)
                    sb.AppendLine();
            }
            return sb.ToString();
        }

        #region Layout & Rendering

        /// <summary>
        /// Report full content height so ScrollViewer knows the scrollable extent.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
            double height = Math.Max(_lines.Count * _lineHeight, _lineHeight);
            return new Size(width, height);
        }

        /// <summary>
        /// Get the parent ScrollViewer's viewport info for determining visible lines.
        /// </summary>
        private (double scrollOffset, double viewportHeight) GetViewportInfo()
        {
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is ScrollViewer sv)
                    return (sv.VerticalOffset, sv.ViewportHeight);
                parent = VisualTreeHelper.GetParent(parent);
            }
            return (0, RenderSize.Height);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var (scrollOffset, viewportHeight) = GetViewportInfo();
            if (viewportHeight <= 0) viewportHeight = RenderSize.Height;

            // Background for visible area (+ buffer to avoid edge flicker)
            double bgTop = Math.Max(0, scrollOffset - _lineHeight);
            double bgBottom = scrollOffset + viewportHeight + _lineHeight;
            dc.DrawRectangle(DefaultBgBrush, null, new Rect(0, bgTop, RenderSize.Width, bgBottom - bgTop));

            if (_lines.Count == 0) return;

            // Only render visible lines for performance
            int firstVisible = Math.Max(0, (int)(scrollOffset / _lineHeight) - 1);
            int lastVisible = Math.Min(_lines.Count - 1, (int)((scrollOffset + viewportHeight) / _lineHeight) + 2);

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            _urlRegions.Clear();

            for (int lineIdx = firstVisible; lineIdx <= lastVisible; lineIdx++)
            {
                double y = lineIdx * _lineHeight; // absolute local coordinates
                var line = _lines[lineIdx];

                string lineText = line.Text;
                var urls = UrlRegex.Matches(lineText);

                DrawLineBackgrounds(dc, line, y);

                if (_hasSelection)
                    DrawSelectionHighlight(dc, lineIdx, y, line.Length);

                DrawLineText(dc, line, y, pixelsPerDip);

                // URL underlines
                foreach (Match m in urls)
                {
                    double ux = m.Index * _charWidth;
                    double uw = m.Length * _charWidth;
                    double underlineY = y + _lineHeight - 2;
                    dc.DrawLine(UrlPen, new Point(ux, underlineY), new Point(ux + uw, underlineY));

                    _urlRegions.Add(new UrlHitRegion
                    {
                        Url = m.Value,
                        Rect = new Rect(ux, y, uw, _lineHeight),
                        Line = lineIdx,
                        StartCol = m.Index,
                        EndCol = m.Index + m.Length
                    });
                }

                // Cursor block
                if (lineIdx == _cursorLine)
                {
                    double cursorX = _cursorCol * _charWidth;
                    dc.DrawRectangle(CursorBrush, null, new Rect(cursorX, y, Math.Max(_charWidth, 2), _lineHeight));
                }
            }
        }

        private void DrawLineBackgrounds(DrawingContext dc, TerminalLine line, double y)
        {
            int col = 0;
            while (col < line.Length)
            {
                var cell = line.Cells[col];
                byte bg = cell.IsInverse ? cell.Fg : cell.Bg;
                if (bg == 0)
                {
                    col++;
                    continue;
                }

                int start = col;
                while (col < line.Length)
                {
                    var c = line.Cells[col];
                    byte cbg = c.IsInverse ? c.Fg : c.Bg;
                    if (cbg != bg) break;
                    col++;
                }

                var rgb = GetCellBgColor(bg);
                var brush = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
                brush.Freeze();
                dc.DrawRectangle(brush, null, new Rect(start * _charWidth, y, (col - start) * _charWidth, _lineHeight));
            }
        }

        private void DrawLineText(DrawingContext dc, TerminalLine line, double y, double pixelsPerDip)
        {
            int col = 0;
            while (col < line.Length)
            {
                var cell = line.Cells[col];
                byte fg = cell.IsInverse ? cell.Bg : cell.Fg;
                byte flags = cell.Flags;
                if (fg == 0 && cell.IsInverse) fg = 1;

                int start = col;
                var runChars = new System.Text.StringBuilder();
                while (col < line.Length)
                {
                    var c = line.Cells[col];
                    byte cfTmp = c.IsInverse ? c.Bg : c.Fg;
                    if (cfTmp == 0 && c.IsInverse) cfTmp = 1;
                    if (cfTmp != fg || c.Flags != flags) break;
                    runChars.Append(c.Char);
                    col++;
                }

                var color = GetCellFgColor(fg, (flags & 1) != 0);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                var typeface = (flags & 1) != 0 ? BoldTypeface : DefaultTypeface;

                var ft = new FormattedText(runChars.ToString(), CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, DefaultFontSize, brush, pixelsPerDip);

                dc.DrawText(ft, new Point(start * _charWidth, y));
            }
        }

        private void DrawSelectionHighlight(DrawingContext dc, int lineIdx, double y, int lineLength)
        {
            int startLine = _selStartLine, startCol = _selStartCol;
            int endLine = _selEndLine, endCol = _selEndCol;

            if (startLine > endLine || (startLine == endLine && startCol > endCol))
            {
                (startLine, endLine) = (endLine, startLine);
                (startCol, endCol) = (endCol, startCol);
            }

            if (lineIdx < startLine || lineIdx > endLine) return;

            int from = (lineIdx == startLine) ? startCol : 0;
            int to = (lineIdx == endLine) ? endCol : Math.Max(lineLength, 1);

            double x = from * _charWidth;
            double w = (to - from) * _charWidth;
            if (w <= 0) return;

            var selBrush = new SolidColorBrush(Color.FromArgb(80, 58, 150, 221));
            selBrush.Freeze();
            dc.DrawRectangle(selBrush, null, new Rect(x, y, w, _lineHeight));
        }

        private static Color GetCellFgColor(byte fg, bool bold)
        {
            if (fg == 0) return DefaultFgColor;

            int index = fg - 1;
            if (bold && index < 8) index += 8;

            if (index < 16)
            {
                var c = TerminalScreenBuffer.GetAnsiColor(index);
                return Color.FromRgb(c.R, c.G, c.B);
            }

            var c256 = TerminalScreenBuffer.Get256Color(index);
            return Color.FromRgb(c256.R, c256.G, c256.B);
        }

        private static Color GetCellBgColor(byte bg)
        {
            if (bg == 0) return DefaultBgColor;

            int index = bg - 1;
            if (index < 16)
            {
                var c = TerminalScreenBuffer.GetAnsiColor(index);
                return Color.FromRgb(c.R, c.G, c.B);
            }

            var c256 = TerminalScreenBuffer.Get256Color(index);
            return Color.FromRgb(c256.R, c256.G, c256.B);
        }

        #endregion

        #region Mouse Interaction (Selection + URL clicking)

        private (int line, int col) HitTest(Point pos)
        {
            // pos is in absolute local coordinates (ScrollViewer handles translation)
            int line = (int)(pos.Y / _lineHeight);
            int col = (int)(pos.X / _charWidth);
            line = Math.Clamp(line, 0, Math.Max(0, _lines.Count - 1));
            col = Math.Max(0, col);
            return (line, col);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            CaptureMouse();
            Focus();

            var pos = e.GetPosition(this);

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var url = FindUrl(pos);
                if (url != null)
                {
                    UrlClicked?.Invoke(url);
                    e.Handled = true;
                    return;
                }
            }

            var (line, col) = HitTest(pos);
            _selStartLine = line;
            _selStartCol = col;
            _selEndLine = line;
            _selEndCol = col;
            _hasSelection = false;
            _isSelecting = true;
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);

            var url = FindUrl(pos);
            if (url != null && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Cursor = Cursors.Hand;
                if (_hoveredUrl == null || _hoveredUrl.Url != url)
                {
                    _hoveredUrl = _urlRegions.FirstOrDefault(u => u.Url == url);
                    InvalidateVisual();
                }
            }
            else
            {
                if (_hoveredUrl != null)
                {
                    _hoveredUrl = null;
                    InvalidateVisual();
                }
                Cursor = Cursors.IBeam;
            }

            if (_isSelecting)
            {
                var (line, col) = HitTest(pos);
                _selEndLine = line;
                _selEndCol = col;
                _hasSelection = _selStartLine != _selEndLine || _selStartCol != _selEndCol;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _isSelecting = false;
            ReleaseMouseCapture();
        }

        private string? FindUrl(Point pos)
        {
            foreach (var region in _urlRegions)
            {
                if (region.Rect.Contains(pos))
                    return region.Url;
            }
            return null;
        }

        #endregion



        private class UrlHitRegion
        {
            public string Url = "";
            public Rect Rect;
            public int Line;
            public int StartCol;
            public int EndCol;
        }

        [GeneratedRegex(@"https?://[^\s<>""'\]）》]+", RegexOptions.Compiled)]
        private static partial Regex CreateUrlRegex();
    }
}
