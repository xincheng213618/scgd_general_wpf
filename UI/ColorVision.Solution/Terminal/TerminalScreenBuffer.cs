using System.Text;

namespace ColorVision.Solution.Terminal
{
    /// <summary>
    /// Minimal VT100 terminal screen buffer for ConPTY output.
    /// Maintains a fixed-size viewport with cursor tracking and scrollback.
    /// Interprets escape sequences to position characters correctly,
    /// so that line-editing echo (PSReadLine, Python REPL, etc.) works properly.
    /// </summary>
    internal class TerminalScreenBuffer
    {
        private readonly int _cols;
        private readonly int _rows;
        private char[][] _screen;
        private int _curRow, _curCol;

        private readonly Queue<string> _scrollbackLines = new();
        private const int MaxScrollbackLines = 3000;

        public TerminalScreenBuffer(int cols = 120, int rows = 30)
        {
            _cols = cols;
            _rows = rows;
            _screen = new char[rows][];
            for (int r = 0; r < rows; r++)
            {
                _screen[r] = new char[cols];
                Array.Fill(_screen[r], ' ');
            }
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
                    _screen[_curRow][_curCol] = c;
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
                _scrollbackLines.Enqueue(LineTrimEnd(_screen[0]));
                if (_scrollbackLines.Count > MaxScrollbackLines)
                    _scrollbackLines.Dequeue();

                // Shift rows up
                for (int r = 1; r < _rows; r++)
                    _screen[r - 1] = _screen[r];
                _screen[_rows - 1] = new char[_cols];
                Array.Fill(_screen[_rows - 1], ' ');
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
                            _screen[_curRow][col] = (col + count < _cols) ? _screen[_curRow][col + count] : ' ';
                        break;
                    }
                case '@': // Insert Characters
                    {
                        int count = Math.Max(1, p0);
                        for (int col = _cols - 1; col >= _curCol + count; col--)
                            _screen[_curRow][col] = _screen[_curRow][col - count];
                        Array.Fill(_screen[_curRow], ' ', _curCol, Math.Min(count, _cols - _curCol));
                        break;
                    }
                case 'X': // Erase Characters
                    {
                        int count = Math.Max(1, p0);
                        Array.Fill(_screen[_curRow], ' ', _curCol, Math.Min(count, _cols - _curCol));
                        break;
                    }
                case 'L': // Insert Lines
                    {
                        int count = Math.Max(1, p0);
                        for (int r = _rows - 1; r >= _curRow + count; r--)
                            _screen[r] = _screen[r - count];
                        for (int r = _curRow; r < Math.Min(_curRow + count, _rows); r++)
                        {
                            _screen[r] = new char[_cols];
                            Array.Fill(_screen[r], ' ');
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
                            _screen[r] = new char[_cols];
                            Array.Fill(_screen[r], ' ');
                        }
                        break;
                    }
                // Sequences we recognize but don't need to act on:
                case 'm': // SGR (colors/bold/etc.) - no color support
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
                    Array.Fill(_screen[_curRow], ' ', _curCol, _cols - _curCol);
                    for (int r = _curRow + 1; r < _rows; r++)
                        Array.Fill(_screen[r], ' ');
                    break;
                case 1: // Erase from start to cursor
                    for (int r = 0; r < _curRow; r++)
                        Array.Fill(_screen[r], ' ');
                    Array.Fill(_screen[_curRow], ' ', 0, Math.Min(_curCol + 1, _cols));
                    break;
                case 2: // Erase entire display
                    for (int r = 0; r < _rows; r++)
                        Array.Fill(_screen[r], ' ');
                    break;
                case 3: // Erase display + scrollback
                    _scrollbackLines.Clear();
                    for (int r = 0; r < _rows; r++)
                        Array.Fill(_screen[r], ' ');
                    break;
            }
        }

        private void HandleEraseLine(int mode)
        {
            switch (mode)
            {
                case 0: // Erase from cursor to end of line
                    Array.Fill(_screen[_curRow], ' ', _curCol, _cols - _curCol);
                    break;
                case 1: // Erase from start of line to cursor
                    Array.Fill(_screen[_curRow], ' ', 0, Math.Min(_curCol + 1, _cols));
                    break;
                case 2: // Erase entire line
                    Array.Fill(_screen[_curRow], ' ');
                    break;
            }
        }

        #endregion

        /// <summary>
        /// Render the full terminal content (scrollback + visible viewport) as a string.
        /// </summary>
        public string Render()
        {
            var sb = new StringBuilder();

            // Scrollback
            foreach (var line in _scrollbackLines)
                sb.Append(line).Append("\r\n");

            // Viewport: find last row that has content or is at/before cursor
            int lastRow = 0;
            for (int r = _rows - 1; r >= 0; r--)
            {
                if (LineTrimEnd(_screen[r]).Length > 0 || r <= _curRow)
                {
                    lastRow = r;
                    break;
                }
            }

            for (int r = 0; r <= lastRow; r++)
            {
                sb.Append(LineTrimEnd(_screen[r]));
                if (r < lastRow)
                    sb.Append("\r\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get the cursor offset in the rendered text (for caret positioning).
        /// </summary>
        public int GetCursorOffset()
        {
            int offset = 0;

            // Scrollback length
            foreach (var line in _scrollbackLines)
                offset += line.Length + 2; // +2 for \r\n

            // Viewport rows before cursor row
            for (int r = 0; r < _curRow; r++)
                offset += LineTrimEnd(_screen[r]).Length + 2;

            // Cursor column in current row
            offset += Math.Min(_curCol, LineTrimEnd(_screen[_curRow]).Length);

            return offset;
        }

        /// <summary>
        /// Get the text content of the current cursor line (trimmed of trailing spaces).
        /// Useful for prompt detection (e.g., ">>> " for Python, "PS C:\> " for PowerShell).
        /// </summary>
        public string GetCurrentLineText()
        {
            return LineTrimEnd(_screen[_curRow]);
        }

        public void Clear()
        {
            _scrollbackLines.Clear();
            for (int r = 0; r < _rows; r++)
                Array.Fill(_screen[r], ' ');
            _curRow = 0;
            _curCol = 0;
        }

        private static string LineTrimEnd(char[] line)
        {
            int end = line.Length;
            while (end > 0 && line[end - 1] == ' ')
                end--;
            return new string(line, 0, end);
        }
    }
}
