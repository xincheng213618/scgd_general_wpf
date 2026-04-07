using System.Text;

namespace ColorVision.Solution.Terminal
{
    /// <summary>
    /// Terminal cell with character and color attribute.
    /// </summary>
    internal struct TerminalCell
    {
        public char Char;
        public byte Fg;   // SGR color index (0-15 for standard, 16-255 for 256-color, 0 = default)
        public byte Bg;   // SGR color index for background
        public byte Flags; // bit0=bold, bit1=underline, bit2=inverse, bit3=dim, bit4=italic

        public bool IsBold => (Flags & 1) != 0;
        public bool IsUnderline => (Flags & 2) != 0;
        public bool IsInverse => (Flags & 4) != 0;

        public static TerminalCell Default => new() { Char = ' ', Fg = 0, Bg = 0, Flags = 0 };
    }

    /// <summary>
    /// A scrollback line with character + color data.
    /// </summary>
    internal class TerminalLine
    {
        public TerminalCell[] Cells;
        public int Length; // trimmed length (excluding trailing spaces)

        public TerminalLine(TerminalCell[] cells)
        {
            Cells = cells;
            Length = cells.Length;
            while (Length > 0 && Cells[Length - 1].Char == ' ' && Cells[Length - 1].Fg == 0 && Cells[Length - 1].Bg == 0 && Cells[Length - 1].Flags == 0)
                Length--;
        }

        public string Text
        {
            get
            {
                var sb = new StringBuilder(Length);
                for (int i = 0; i < Length; i++)
                    sb.Append(Cells[i].Char);
                return sb.ToString();
            }
        }
    }

    /// <summary>
    /// VT100 terminal screen buffer with per-cell color attributes.
    /// Maintains a fixed-size viewport with cursor tracking and scrollback.
    /// Interprets escape sequences including SGR colors.
    /// </summary>
    internal class TerminalScreenBuffer
    {
        private readonly int _cols;
        private readonly int _rows;
        private TerminalCell[][] _screen;
        private int _curRow, _curCol;

        // Current SGR state
        private byte _curFg;
        private byte _curBg;
        private byte _curFlags;

        private readonly Queue<TerminalLine> _scrollbackLines = new();
        private const int MaxScrollbackLines = 3000;

        public int Cols => _cols;
        public int Rows => _rows;
        public int CursorRow => _curRow;
        public int CursorCol => _curCol;

        public TerminalScreenBuffer(int cols = 120, int rows = 30)
        {
            _cols = cols;
            _rows = rows;
            _screen = new TerminalCell[rows][];
            for (int r = 0; r < rows; r++)
            {
                _screen[r] = new TerminalCell[cols];
                FillRow(_screen[r]);
            }
        }

        private void FillRow(TerminalCell[] row)
        {
            for (int i = 0; i < row.Length; i++)
                row[i] = TerminalCell.Default;
        }

        /// <summary>
        /// Process raw ConPTY output (including VT100 escape sequences).
        /// </summary>
        public void Write(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '\x1b')
                {
                    i = HandleEscape(text, i);
                }
                else if (c == '\r')
                {
                    _curCol = 0;
                    i++;
                }
                else if (c == '\n')
                {
                    LineFeed();
                    i++;
                }
                else if (c == '\b' || c == '\x08')
                {
                    if (_curCol > 0) _curCol--;
                    i++;
                }
                else if (c == '\t')
                {
                    _curCol = Math.Min(((_curCol / 8) + 1) * 8, _cols - 1);
                    i++;
                }
                else if (c < ' ' || c == '\x7f')
                {
                    i++; // skip other control chars (BEL, NUL, DEL, etc.)
                }
                else
                {
                    // Printable character
                    if (_curCol >= _cols)
                    {
                        _curCol = 0;
                        LineFeed();
                    }
                    _screen[_curRow][_curCol] = new TerminalCell
                    {
                        Char = c,
                        Fg = _curFg,
                        Bg = _curBg,
                        Flags = _curFlags
                    };
                    _curCol++;
                    i++;
                }
            }
        }

        private void LineFeed()
        {
            if (_curRow < _rows - 1)
            {
                _curRow++;
            }
            else
            {
                // Scroll up: push first line to scrollback
                _scrollbackLines.Enqueue(new TerminalLine(_screen[0]));
                if (_scrollbackLines.Count > MaxScrollbackLines)
                    _scrollbackLines.Dequeue();

                // Shift rows up
                for (int r = 1; r < _rows; r++)
                    _screen[r - 1] = _screen[r];
                _screen[_rows - 1] = new TerminalCell[_cols];
                FillRow(_screen[_rows - 1]);
            }
        }

        #region Escape Sequence Handling

        private int HandleEscape(string text, int pos)
        {
            pos++; // skip ESC
            if (pos >= text.Length) return pos;

            char c = text[pos];
            switch (c)
            {
                case '[':
                    return HandleCSI(text, pos + 1);
                case ']':
                    return HandleOSC(text, pos + 1);
                case '(' or ')':
                    return Math.Min(pos + 2, text.Length); // charset designation
                case 'M': // Reverse index
                    if (_curRow > 0) _curRow--;
                    return pos + 1;
                case 'D': // Index (move down / scroll)
                    LineFeed();
                    return pos + 1;
                case 'E': // Next line
                    _curCol = 0;
                    LineFeed();
                    return pos + 1;
                default:
                    return pos + 1; // single-char escape
            }
        }

        private static int HandleOSC(string text, int pos)
        {
            // Skip until BEL (\x07) or ST (ESC \)
            while (pos < text.Length)
            {
                if (text[pos] == '\x07') return pos + 1;
                if (text[pos] == '\x1b' && pos + 1 < text.Length && text[pos + 1] == '\\')
                    return pos + 2;
                pos++;
            }
            return pos;
        }

        private int HandleCSI(string text, int pos)
        {
            // Check for private mode marker (?, >, =)
            bool privateMode = false;
            if (pos < text.Length && (text[pos] == '?' || text[pos] == '>' || text[pos] == '='))
            {
                privateMode = true;
                pos++;
            }

            // Parse numeric parameters
            var parms = new List<int>();
            int num = 0;
            bool hasNum = false;
            while (pos < text.Length)
            {
                char ch = text[pos];
                if (ch >= '0' && ch <= '9')
                {
                    num = num * 10 + (ch - '0');
                    hasNum = true;
                    pos++;
                }
                else if (ch == ';')
                {
                    parms.Add(hasNum ? num : 0);
                    num = 0;
                    hasNum = false;
                    pos++;
                }
                else break;
            }
            if (hasNum) parms.Add(num);

            // Skip intermediate bytes (0x20-0x2F)
            while (pos < text.Length && text[pos] >= 0x20 && text[pos] <= 0x2F)
                pos++;

            // Read final byte
            if (pos >= text.Length) return pos;
            char cmd = text[pos];
            pos++;

            if (privateMode) return pos; // ignore DEC private mode sequences

            int p0 = parms.Count > 0 ? parms[0] : 0;
            int p1 = parms.Count > 1 ? parms[1] : 0;

            switch (cmd)
            {
                case 'A': // Cursor Up
                    _curRow = Math.Max(0, _curRow - Math.Max(1, p0));
                    break;
                case 'B': // Cursor Down
                    _curRow = Math.Min(_rows - 1, _curRow + Math.Max(1, p0));
                    break;
                case 'C': // Cursor Forward
                    _curCol = Math.Min(_cols - 1, _curCol + Math.Max(1, p0));
                    break;
                case 'D': // Cursor Back
                    _curCol = Math.Max(0, _curCol - Math.Max(1, p0));
                    break;
                case 'E': // Cursor Next Line
                    _curRow = Math.Min(_rows - 1, _curRow + Math.Max(1, p0));
                    _curCol = 0;
                    break;
                case 'F': // Cursor Previous Line
                    _curRow = Math.Max(0, _curRow - Math.Max(1, p0));
                    _curCol = 0;
                    break;
                case 'G': // Cursor Horizontal Absolute
                    _curCol = Math.Clamp((p0 > 0 ? p0 : 1) - 1, 0, _cols - 1);
                    break;
                case 'H' or 'f': // Cursor Position
                    _curRow = Math.Clamp((p0 > 0 ? p0 : 1) - 1, 0, _rows - 1);
                    _curCol = Math.Clamp((p1 > 0 ? p1 : 1) - 1, 0, _cols - 1);
                    break;
                case 'd': // Cursor Vertical Absolute
                    _curRow = Math.Clamp((p0 > 0 ? p0 : 1) - 1, 0, _rows - 1);
                    break;
                case 'J': // Erase in Display
                    HandleEraseDisplay(p0);
                    break;
                case 'K': // Erase in Line
                    HandleEraseLine(p0);
                    break;
                case 'P': // Delete Characters
                    {
                        int count = Math.Max(1, p0);
                        for (int col = _curCol; col < _cols; col++)
                            _screen[_curRow][col] = (col + count < _cols) ? _screen[_curRow][col + count] : TerminalCell.Default;
                        break;
                    }
                case '@': // Insert Characters
                    {
                        int count = Math.Max(1, p0);
                        for (int col = _cols - 1; col >= _curCol + count; col--)
                            _screen[_curRow][col] = _screen[_curRow][col - count];
                        for (int col = _curCol; col < Math.Min(_curCol + count, _cols); col++)
                            _screen[_curRow][col] = TerminalCell.Default;
                        break;
                    }
                case 'X': // Erase Characters
                    {
                        int count = Math.Max(1, p0);
                        for (int col = _curCol; col < Math.Min(_curCol + count, _cols); col++)
                            _screen[_curRow][col] = TerminalCell.Default;
                        break;
                    }
                case 'L': // Insert Lines
                    {
                        int count = Math.Max(1, p0);
                        for (int r = _rows - 1; r >= _curRow + count; r--)
                            _screen[r] = _screen[r - count];
                        for (int r = _curRow; r < Math.Min(_curRow + count, _rows); r++)
                        {
                            _screen[r] = new TerminalCell[_cols];
                            FillRow(_screen[r]);
                        }
                        break;
                    }
                case 'M': // Delete Lines
                    {
                        int count = Math.Max(1, p0);
                        for (int r = _curRow; r < _rows - count; r++)
                            _screen[r] = _screen[r + count];
                        for (int r = Math.Max(_rows - count, _curRow); r < _rows; r++)
                        {
                            _screen[r] = new TerminalCell[_cols];
                            FillRow(_screen[r]);
                        }
                        break;
                    }
                case 'm': // SGR (colors/bold/etc.)
                    HandleSGR(parms);
                    break;
                // Sequences we recognize but don't need to act on:
                case 'r': // Set scrolling region
                case 'h': // Set mode
                case 'l': // Reset mode
                case 'n': // Device status report
                case 's': // Save cursor position
                case 'u': // Restore cursor position
                case 'S': // Scroll up
                case 'T': // Scroll down
                    break;
            }

            return pos;
        }

        private void HandleEraseDisplay(int mode)
        {
            switch (mode)
            {
                case 0: // Erase from cursor to end of display
                    for (int col = _curCol; col < _cols; col++)
                        _screen[_curRow][col] = TerminalCell.Default;
                    for (int r = _curRow + 1; r < _rows; r++)
                        FillRow(_screen[r]);
                    break;
                case 1: // Erase from start to cursor
                    for (int r = 0; r < _curRow; r++)
                        FillRow(_screen[r]);
                    for (int col = 0; col <= Math.Min(_curCol, _cols - 1); col++)
                        _screen[_curRow][col] = TerminalCell.Default;
                    break;
                case 2: // Erase entire display
                    for (int r = 0; r < _rows; r++)
                        FillRow(_screen[r]);
                    break;
                case 3: // Erase display + scrollback
                    _scrollbackLines.Clear();
                    for (int r = 0; r < _rows; r++)
                        FillRow(_screen[r]);
                    break;
            }
        }

        private void HandleEraseLine(int mode)
        {
            switch (mode)
            {
                case 0: // Erase from cursor to end of line
                    for (int col = _curCol; col < _cols; col++)
                        _screen[_curRow][col] = TerminalCell.Default;
                    break;
                case 1: // Erase from start of line to cursor
                    for (int col = 0; col <= Math.Min(_curCol, _cols - 1); col++)
                        _screen[_curRow][col] = TerminalCell.Default;
                    break;
                case 2: // Erase entire line
                    FillRow(_screen[_curRow]);
                    break;
            }
        }

        #endregion

        /// <summary>
        /// Handle SGR (Select Graphic Rendition) escape sequences for colors and text attributes.
        /// </summary>
        private void HandleSGR(List<int> parms)
        {
            if (parms.Count == 0) parms.Add(0);

            for (int i = 0; i < parms.Count; i++)
            {
                int p = parms[i];
                switch (p)
                {
                    case 0: // Reset
                        _curFg = 0; _curBg = 0; _curFlags = 0;
                        break;
                    case 1: // Bold
                        _curFlags |= 1;
                        break;
                    case 2: // Dim
                        _curFlags |= 8;
                        break;
                    case 3: // Italic
                        _curFlags |= 16;
                        break;
                    case 4: // Underline
                        _curFlags |= 2;
                        break;
                    case 7: // Inverse
                        _curFlags |= 4;
                        break;
                    case 22: // Normal intensity (not bold, not dim)
                        _curFlags = (byte)(_curFlags & ~(1 | 8));
                        break;
                    case 23: // Not italic
                        _curFlags = (byte)(_curFlags & ~16);
                        break;
                    case 24: // Not underline
                        _curFlags = (byte)(_curFlags & ~2);
                        break;
                    case 27: // Not inverse
                        _curFlags = (byte)(_curFlags & ~4);
                        break;
                    // Standard foreground colors (30-37)
                    case >= 30 and <= 37:
                        _curFg = (byte)(p - 30 + 1); // 1-8
                        break;
                    case 38: // Extended foreground color
                        if (i + 1 < parms.Count && parms[i + 1] == 5 && i + 2 < parms.Count)
                        {
                            _curFg = (byte)(parms[i + 2] + 1); // offset by 1, 0 = default
                            i += 2;
                        }
                        else if (i + 1 < parms.Count && parms[i + 1] == 2 && i + 4 < parms.Count)
                        {
                            // 24-bit: approximate to nearest 256-color
                            int r = parms[i + 2], g = parms[i + 3], b = parms[i + 4];
                            _curFg = (byte)(Rgb24To256(r, g, b) + 1);
                            i += 4;
                        }
                        break;
                    case 39: // Default foreground
                        _curFg = 0;
                        break;
                    // Standard background colors (40-47)
                    case >= 40 and <= 47:
                        _curBg = (byte)(p - 40 + 1);
                        break;
                    case 48: // Extended background color
                        if (i + 1 < parms.Count && parms[i + 1] == 5 && i + 2 < parms.Count)
                        {
                            _curBg = (byte)(parms[i + 2] + 1);
                            i += 2;
                        }
                        else if (i + 1 < parms.Count && parms[i + 1] == 2 && i + 4 < parms.Count)
                        {
                            int r = parms[i + 2], g = parms[i + 3], b = parms[i + 4];
                            _curBg = (byte)(Rgb24To256(r, g, b) + 1);
                            i += 4;
                        }
                        break;
                    case 49: // Default background
                        _curBg = 0;
                        break;
                    // Bright foreground colors (90-97)
                    case >= 90 and <= 97:
                        _curFg = (byte)(p - 90 + 9); // 9-16
                        break;
                    // Bright background colors (100-107)
                    case >= 100 and <= 107:
                        _curBg = (byte)(p - 100 + 9);
                        break;
                }
            }
        }

        private static int Rgb24To256(int r, int g, int b)
        {
            // Check grayscale ramp first
            if (r == g && g == b)
            {
                if (r < 8) return 16;
                if (r > 248) return 231;
                return (int)Math.Round((r - 8) / 247.0 * 23) + 232;
            }
            // 6x6x6 color cube
            int ri = (int)Math.Round(r / 255.0 * 5);
            int gi = (int)Math.Round(g / 255.0 * 5);
            int bi = (int)Math.Round(b / 255.0 * 5);
            return 16 + 36 * ri + 6 * gi + bi;
        }

        /// <summary>
        /// Render the full terminal content (scrollback + visible viewport) as a string (plain text, no colors).
        /// </summary>
        public string Render()
        {
            var sb = new StringBuilder();

            // Scrollback
            foreach (var line in _scrollbackLines)
                sb.Append(line.Text).Append("\r\n");

            // Viewport: find last row that has content or is at/before cursor
            int lastRow = 0;
            for (int r = _rows - 1; r >= 0; r--)
            {
                if (GetRowLength(r) > 0 || r <= _curRow)
                {
                    lastRow = r;
                    break;
                }
            }

            for (int r = 0; r <= lastRow; r++)
            {
                sb.Append(GetRowText(r));
                if (r < lastRow)
                    sb.Append("\r\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get all lines (scrollback + viewport) for rendering with colors.
        /// Returns a list of (cells, length) for each line.
        /// </summary>
        public (List<TerminalLine> lines, int cursorLine, int cursorCol) RenderLines()
        {
            var result = new List<TerminalLine>();

            foreach (var line in _scrollbackLines)
                result.Add(line);

            // Viewport: find last row with content
            int lastRow = 0;
            for (int r = _rows - 1; r >= 0; r--)
            {
                if (GetRowLength(r) > 0 || r <= _curRow)
                {
                    lastRow = r;
                    break;
                }
            }

            for (int r = 0; r <= lastRow; r++)
                result.Add(new TerminalLine(_screen[r]));

            int cursorLine = _scrollbackLines.Count + _curRow;
            return (result, cursorLine, _curCol);
        }

        /// <summary>
        /// Get the cursor offset in the rendered text (for caret positioning).
        /// </summary>
        public int GetCursorOffset()
        {
            int offset = 0;

            // Scrollback length
            foreach (var line in _scrollbackLines)
                offset += line.Text.Length + 2; // +2 for \r\n

            // Viewport rows before cursor row
            for (int r = 0; r < _curRow; r++)
                offset += GetRowText(r).Length + 2;

            // Cursor column in current row
            offset += Math.Min(_curCol, GetRowLength(_curRow));

            return offset;
        }

        /// <summary>
        /// Get the text content of the current cursor line.
        /// </summary>
        public string GetCurrentLineText()
        {
            return GetRowText(_curRow);
        }

        public void Clear()
        {
            _scrollbackLines.Clear();
            for (int r = 0; r < _rows; r++)
                FillRow(_screen[r]);
            _curRow = 0;
            _curCol = 0;
            _curFg = 0;
            _curBg = 0;
            _curFlags = 0;
        }

        private int GetRowLength(int row)
        {
            int end = _cols;
            while (end > 0 && _screen[row][end - 1].Char == ' '
                && _screen[row][end - 1].Fg == 0 && _screen[row][end - 1].Bg == 0
                && _screen[row][end - 1].Flags == 0)
                end--;
            return end;
        }

        private string GetRowText(int row)
        {
            int len = GetRowLength(row);
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
                sb.Append(_screen[row][i].Char);
            return sb.ToString();
        }

        /// <summary>
        /// Standard 16-color palette (indices 0-15). Index 0 here maps to SGR color index 1.
        /// Returns (R, G, B) tuple.
        /// </summary>
        public static (byte R, byte G, byte B) GetAnsiColor(int index)
        {
            return index switch
            {
                0 => (0, 0, 0),         // Black
                1 => (205, 49, 49),      // Red
                2 => (13, 188, 121),     // Green
                3 => (229, 229, 16),     // Yellow
                4 => (36, 114, 200),     // Blue
                5 => (188, 63, 188),     // Magenta
                6 => (17, 168, 205),     // Cyan
                7 => (204, 204, 204),    // White
                8 => (118, 118, 118),    // Bright Black
                9 => (241, 76, 76),      // Bright Red
                10 => (35, 209, 139),    // Bright Green
                11 => (245, 245, 67),    // Bright Yellow
                12 => (59, 142, 234),    // Bright Blue
                13 => (214, 112, 214),   // Bright Magenta
                14 => (41, 184, 219),    // Bright Cyan
                15 => (229, 229, 229),   // Bright White
                _ => (204, 204, 204)     // fallback
            };
        }

        /// <summary>
        /// Get RGB color for a 256-color index (0-255).
        /// </summary>
        public static (byte R, byte G, byte B) Get256Color(int index)
        {
            if (index < 16)
                return GetAnsiColor(index);

            if (index < 232)
            {
                // 6x6x6 color cube
                index -= 16;
                int b = index % 6;
                index /= 6;
                int g = index % 6;
                int r = index / 6;
                return ((byte)(r == 0 ? 0 : 55 + r * 40),
                        (byte)(g == 0 ? 0 : 55 + g * 40),
                        (byte)(b == 0 ? 0 : 55 + b * 40));
            }

            // Grayscale ramp (232-255)
            byte v = (byte)(8 + (index - 232) * 10);
            return (v, v, v);
        }
    }
}
